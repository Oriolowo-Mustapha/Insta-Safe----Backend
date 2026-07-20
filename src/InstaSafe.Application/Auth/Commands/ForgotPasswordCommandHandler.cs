using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Auth.Commands;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;

    public ForgotPasswordCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork, IEmailService emailService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
    }

    public async Task<Result<string>> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        // Don't reveal if user exists or not for security
        if (user == null)
            return Result<string>.Success("If an account with that email exists, a password reset link has been sent.");

        var resetToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        user.SetPasswordResetToken(resetToken, DateTime.UtcNow.AddHours(1));

        await _unitOfWork.SaveChangesAsync(ct);

        var resetLink = $"http://localhost:5173/auth/reset-password?email={user.Email}&token={Uri.EscapeDataString(resetToken)}";
        await _emailService.SendEmailAsync(user.Email, "Reset your InstaSafe Password", 
            $"Please reset your password by clicking this link: <a href=\"{resetLink}\">{resetLink}</a>. This link will expire in 1 hour.", ct);

        return Result<string>.Success("If an account with that email exists, a password reset link has been sent.");
    }
}
