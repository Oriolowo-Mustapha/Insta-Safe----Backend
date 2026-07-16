using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Auth.Commands;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyEmailCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<string>> Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user == null)
            return Result<string>.Failure("User not found.");

        if (user.IsEmailVerified)
            return Result<string>.Failure("Email is already verified.");

        if (user.EmailVerificationToken != request.Token)
            return Result<string>.Failure("Invalid verification token.");

        user.VerifyEmail();
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<string>.Success("Email verified successfully.");
    }
}
