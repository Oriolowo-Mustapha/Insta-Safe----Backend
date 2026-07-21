using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Configuration;

namespace InstaSafe.Application.Auth.Commands;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public ForgotPasswordCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork, IEmailService emailService, IConfiguration configuration)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _configuration = configuration;
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

        var frontendUrl = _configuration["FrontendUrl:Production"] ?? "https://instasafe.vercel.app";
        var resetLink = $"{frontendUrl}/auth/reset-password?email={user.Email}&token={Uri.EscapeDataString(resetToken)}";
        await _emailService.SendEmailAsync(user.Email, "Reset your InstaSafe Password", 
            $"Please reset your password by clicking this link: <a href=\"{resetLink}\">{resetLink}</a>. This link will expire in 1 hour.", ct);

        return Result<string>.Success("If an account with that email exists, a password reset link has been sent.");
    }
}
