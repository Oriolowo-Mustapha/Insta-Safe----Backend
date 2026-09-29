using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Features.Auth.Commands.RequestEmailCode;

public class RequestEmailCodeCommandHandler : IRequestHandler<RequestEmailCodeCommand, Result<bool>>
{
    private readonly IVendorRepository _vendors;
    private readonly IOtpService _otp;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;
    private readonly ILogger<RequestEmailCodeCommandHandler> _logger;

    public RequestEmailCodeCommandHandler(
        IVendorRepository vendors, IOtpService otp, IEmailSender email,
        IConfiguration config, ILogger<RequestEmailCodeCommandHandler> logger)
    {
        _vendors = vendors; _otp = otp; _email = email; _config = config; _logger = logger;
    }

    public async Task<Result<bool>> Handle(RequestEmailCodeCommand req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var vendor = await _vendors.GetByEmailAsync(email, ct);
        if (vendor is null || !vendor.IsActive)
            return Result<bool>.Failure("No active vendor found for this email.");
        if (vendor.EmailVerified)
            return Result<bool>.Failure("Email is already verified.");

        var code = _otp.GenerateOtp();
        var salt = _otp.NewSalt();
        vendor.EmailOtpHash = _otp.Hash(code, salt) + "." + salt;
        var minutes = int.TryParse(_config["Auth:OtpMinutes"], out var m) && m > 0 ? m : 10;
        vendor.EmailOtpExpiresAt = DateTimeOffset.UtcNow.AddMinutes(minutes);
        vendor.EmailOtpAttempts = 0;
        vendor.Touch();
        await _vendors.SaveAsync(ct);

        try
        {
            await _email.SendAsync(email,
                "Verify your InstaSafe email",
                $"<p>Hi {vendor.FirstName ?? vendor.DisplayName},</p><p>Your verification code is <b>{code}</b>. Valid for {minutes} minutes.</p>",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Verification email resend failed for {Email}", email);
            return Result<bool>.Failure("Could not send verification email. Try again shortly.");
        }

        return Result<bool>.Success(true);
    }
}
