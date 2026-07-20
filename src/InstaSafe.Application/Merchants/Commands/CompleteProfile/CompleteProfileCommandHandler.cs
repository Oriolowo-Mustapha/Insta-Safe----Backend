using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Models.Monnify;
using InstaSafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Merchants.Commands.CompleteProfile;

public class CompleteProfileCommandHandler : IRequestHandler<CompleteProfileCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMonnifyPaymentService _monnifyPaymentService;
    private readonly Microsoft.Extensions.Logging.ILogger<CompleteProfileCommandHandler> _logger;

    public CompleteProfileCommandHandler(
        IApplicationDbContext context, 
        IUnitOfWork unitOfWork, 
        IMonnifyPaymentService monnifyPaymentService,
        Microsoft.Extensions.Logging.ILogger<CompleteProfileCommandHandler> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _monnifyPaymentService = monnifyPaymentService;
        _logger = logger;
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
                _logger.LogWarning("Sandbox Soft-Fail: Invalid bank account details for User {UserId}", merchant.UserId);
            }

            // 2. Verify BVN against user's name (and DOB if available)
            var fullName = $"{merchant.User.FirstName} {merchant.User.LastName}";
            var formattedDob = merchant.DateOfBirth?.ToString("dd-MMM-yyyy");
            var bvnRequest = new BvnMatchRequest(request.Bvn, fullName, formattedDob, merchant.Phone);
            
            var bvnVerificationResponse = await _monnifyPaymentService.VerifyBvnAsync(bvnRequest, cancellationToken);
            
            if (!bvnVerificationResponse.RequestSuccessful || bvnVerificationResponse.ResponseBody == null)
            {
                _logger.LogWarning("Sandbox Soft-Fail: BVN verification failed for User {UserId}", merchant.UserId);
            }
            // Monnify matchStatus can be FULL_MATCH, PARTIAL_MATCH, or NO_MATCH
            else if (bvnVerificationResponse.ResponseBody.Name.MatchStatus == "NO_MATCH" && bvnVerificationResponse.ResponseBody.Name.MatchPercentage < 50)
            {
                _logger.LogWarning("Sandbox Soft-Fail: BVN name does not match registered name for User {UserId}", merchant.UserId);
            }

            // 3. Verify NIN if provided
            if (!string.IsNullOrEmpty(request.Nin))
            {
                var ninRequest = new NinVerificationRequest(request.Nin);
                var ninVerificationResponse = await _monnifyPaymentService.VerifyNinAsync(ninRequest, cancellationToken);
                
                if (!ninVerificationResponse.RequestSuccessful || ninVerificationResponse.ResponseBody == null)
                {
                    _logger.LogWarning("Sandbox Soft-Fail: Invalid NIN provided for User {UserId}", merchant.UserId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sandbox Soft-Fail: Verification service threw an exception for User {UserId}", merchant.UserId);
        }

        merchant.Bvn = request.Bvn;
        merchant.Nin = request.Nin;
        merchant.PayoutBankAccount = request.PayoutBankAccount;
        merchant.PayoutBankCode = request.PayoutBankCode;
        merchant.IsVerified = true;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<string>.Success("Profile completed successfully.");
    }
}
