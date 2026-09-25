using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Features.Dispatch.Commands.RequestDispatcherOtp;

public class RequestDispatcherOtpCommandHandler : IRequestHandler<RequestDispatcherOtpCommand, Result<bool>>
{
    private readonly IDispatcherRepository _dispatchers;
    private readonly IOtpService _otp;
    private readonly IWhatsAppSender _wa;
    private readonly IConfiguration _config;
    private readonly ILogger<RequestDispatcherOtpCommandHandler> _logger;

    public RequestDispatcherOtpCommandHandler(
        IDispatcherRepository dispatchers, IOtpService otp, IWhatsAppSender wa,
        IConfiguration config, ILogger<RequestDispatcherOtpCommandHandler> logger)
    {
        _dispatchers = dispatchers; _otp = otp; _wa = wa; _config = config; _logger = logger;
    }

    public async Task<Result<bool>> Handle(RequestDispatcherOtpCommand req, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(req.Phone);
        if (string.IsNullOrWhiteSpace(phone))
            return Result<bool>.Failure("Phone number is required.");

        // Phone-only identity: auto-provision the row on first OTP request.
        var dispatcher = await _dispatchers.GetByPhoneAsync(phone, ct);
        if (dispatcher is null)
        {
            dispatcher = new Dispatcher { Phone = phone, IsActive = true };
            await _dispatchers.AddAsync(dispatcher, ct);
        }
        if (!dispatcher.IsActive)
            return Result<bool>.Failure("This driver account is deactivated.");

        var code = _otp.GenerateOtp();
        var salt = _otp.NewSalt();
        dispatcher.OtpHash = _otp.Hash(code, salt) + "." + salt;
        var minutes = int.TryParse(_config["Auth:OtpMinutes"], out var m) && m > 0 ? m : 10;
        dispatcher.OtpExpiresAt = DateTimeOffset.UtcNow.AddMinutes(minutes);
        dispatcher.OtpAttempts = 0;
        dispatcher.Touch();
        await _dispatchers.SaveAsync(ct);

        try
        {
            await _wa.SendTextAsync(phone,
                $"InstaSafe driver login code: {code}. Valid for {minutes} minutes. Never share it.", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dispatcher OTP WhatsApp send failed for {Phone}", phone);
        }

        return Result<bool>.Success(true);
    }
}
