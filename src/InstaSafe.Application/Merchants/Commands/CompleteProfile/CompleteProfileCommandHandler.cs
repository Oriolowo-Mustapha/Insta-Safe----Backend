using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Models.Monnify;
using InstaSafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Merchants.Commands.CompleteProfile;

public class CompleteProfileCommandHandler : IRequestHandler<CompleteProfileCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMonnifyPaymentService _monnifyPaymentService;

    public CompleteProfileCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork, IMonnifyPaymentService monnifyPaymentService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _monnifyPaymentService = monnifyPaymentService;
    }

    public async Task<Result<string>> Handle(CompleteProfileCommand request, CancellationToken cancellationToken)
    {
        var merchant = await _context.Merchants
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.UserId == request.MerchantUserId, cancellationToken);

        if (merchant == null)
            return Result<string>.Failure("Merchant not found.");

        if (request.Bvn.Length != 11 || !request.Bvn.All(char.IsDigit))
            return Result<string>.Failure("BVN must be exactly 11 digits.");

        if (request.PayoutBankAccount.Length != 10 || !request.PayoutBankAccount.All(char.IsDigit))
            return Result<string>.Failure("Bank Account must be exactly 10 digits.");

        try
        {
            // 1. Verify Bank Account
            var accountVerificationResponse = await _monnifyPaymentService.VerifyAccountAsync(request.PayoutBankAccount, request.PayoutBankCode, cancellationToken);
            if (!accountVerificationResponse.RequestSuccessful)
            {
                return Result<string>.Failure("Invalid bank account details.");
            }

            // 2. Verify BVN against user's name (and DOB if available)
            var fullName = $"{merchant.User.FirstName} {merchant.User.LastName}";
            var formattedDob = merchant.DateOfBirth?.ToString("dd-MMM-yyyy");
            var bvnRequest = new BvnMatchRequest(request.Bvn, fullName, formattedDob, merchant.Phone);
            
            var bvnVerificationResponse = await _monnifyPaymentService.VerifyBvnAsync(bvnRequest, cancellationToken);
            
            if (!bvnVerificationResponse.RequestSuccessful || bvnVerificationResponse.ResponseBody == null)
            {
                return Result<string>.Failure("BVN verification failed.");
            }

            // Monnify matchStatus can be FULL_MATCH, PARTIAL_MATCH, or NO_MATCH
            if (bvnVerificationResponse.ResponseBody.Name.MatchStatus == "NO_MATCH" && bvnVerificationResponse.ResponseBody.Name.MatchPercentage < 50)
            {
                return Result<string>.Failure("BVN name does not match registered name.");
            }

            // 3. Verify NIN if provided
            if (!string.IsNullOrEmpty(request.Nin))
            {
                var ninRequest = new NinVerificationRequest(request.Nin);
                var ninVerificationResponse = await _monnifyPaymentService.VerifyNinAsync(ninRequest, cancellationToken);
                
                if (!ninVerificationResponse.RequestSuccessful || ninVerificationResponse.ResponseBody == null)
                {
                    return Result<string>.Failure("Invalid NIN provided.");
                }
            }
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Verification service is currently unavailable. {ex.Message}");
        }

        merchant.Bvn = request.Bvn;
        merchant.Nin = request.Nin;
        merchant.PayoutBankAccount = request.PayoutBankAccount;
        merchant.PayoutBankCode = request.PayoutBankCode;
        merchant.IsVerified = true;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<string>.Success("Profile completed successfully.");
    }
}
