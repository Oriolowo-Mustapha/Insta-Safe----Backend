using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Auth.Commands;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<string>> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user == null)
            return Result<string>.Failure("Invalid request.");

        if (user.PasswordResetToken != request.Token || 
            user.PasswordResetTokenExpires == null || 
            user.PasswordResetTokenExpires < DateTime.UtcNow)
        {
            return Result<string>.Failure("Invalid or expired token.");
        }

        var newHash = _passwordHasher.Hash(request.NewPassword);
        user.ResetPassword(newHash);

        await _unitOfWork.SaveChangesAsync(ct);

        return Result<string>.Success("Password reset successfully.");
    }
}
