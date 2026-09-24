using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Features.Auth.Commands.RequestVendorOtp;

public class RequestVendorOtpCommandHandler : IRequestHandler<RequestVendorOtpCommand, Result<bool>>
{
    private readonly IVendorRepository _vendors;
    private readonly IOtpService _otp;
    private readonly IWhatsAppSender _wa;
    private readonly IConfiguration _config;
    private readonly ILogger<RequestVendorOtpCommandHandler> _logger;

    public RequestVendorOtpCommandHandler(
        IVendorRepository vendors, IOtpService otp, IWhatsAppSender wa,
        IConfiguration config, ILogger<RequestVendorOtpCommandHandler> logger)
    {
        _vendors = vendors; _otp = otp; _wa = wa; _config = config; _logger = logger;
    }

    public async Task<Result<bool>> Handle(RequestVendorOtpCommand req, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(req.Phone);
        var vendor = await _vendors.GetByPhoneAsync(phone, ct);
        if (vendor is null || !vendor.IsActive)
            return Result<bool>.Failure("No active vendor found for this phone. Register first.");

        var code = _otp.GenerateOtp();
        var salt = _otp.NewSalt();
        vendor.OtpHash = _otp.Hash(code, salt) + "." + salt;
        var minutes = int.TryParse(_config["Auth:OtpMinutes"], out var m) && m > 0 ? m : 10;
        vendor.OtpExpiresAt = DateTimeOffset.UtcNow.AddMinutes(minutes);
        vendor.OtpAttempts = 0;
        vendor.Touch();
        await _vendors.SaveAsync(ct);

        try
        {
            await _wa.SendTextAsync(phone,
                $"InstaSafe login code: {code}. Valid for {minutes} minutes. Never share it.", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vendor OTP WhatsApp send failed for {Phone}", phone);
        }

        return Result<bool>.Success(true);
    }
}
