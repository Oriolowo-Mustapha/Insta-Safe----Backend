using AutoMapper;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using InstaSafe.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Features.Vendors.Commands.RegisterVendor;

public class RegisterVendorCommandHandler : IRequestHandler<RegisterVendorCommand, Result<VendorDto>>
{
    private readonly IVendorRepository _vendors;
    private readonly ISanitizer _sanitizer;
    private readonly IPasswordHasher _passwords;
    private readonly IOtpService _otp;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;
    private readonly ILogger<RegisterVendorCommandHandler> _logger;
    private readonly IMapper _mapper;

    public RegisterVendorCommandHandler(
        IVendorRepository vendors, ISanitizer sanitizer,
        IPasswordHasher passwords, IOtpService otp, IEmailSender email,
        IConfiguration config, ILogger<RegisterVendorCommandHandler> logger, IMapper mapper)
    {
        _vendors = vendors; _sanitizer = sanitizer;
        _passwords = passwords; _otp = otp; _email = email;
        _config = config; _logger = logger; _mapper = mapper;
    }

    public async Task<Result<VendorDto>> Handle(RegisterVendorCommand req, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(req.Phone);
        if (string.IsNullOrWhiteSpace(phone))
            return Result<VendorDto>.Failure("Vendor phone is required.");

        if (await _vendors.ExistsByPhoneAsync(phone, ct))
            return Result<VendorDto>.Failure($"Vendor with phone '{phone}' already exists.");

        var email = req.Email.Trim().ToLowerInvariant();
        if (await _vendors.ExistsByEmailAsync(email, ct))
            return Result<VendorDto>.Failure($"Vendor with email '{email}' already exists.");

        var vendor = new Vendor
        {
            Phone = _sanitizer.Clean(phone, 20),
            DisplayName = _sanitizer.Clean(req.DisplayName, 120),
            FirstName = _sanitizer.Clean(req.FirstName, 120),
            LastName = _sanitizer.Clean(req.LastName, 120),
            Email = _sanitizer.Clean(email, 200),
            PasswordHash = _passwords.Hash(req.Password),
            IsActive = true,
            EmailVerified = false,
            OnboardingCompleted = false
        };

        var code = _otp.GenerateOtp();
        var salt = _otp.NewSalt();
        vendor.EmailOtpHash = _otp.Hash(code, salt) + "." + salt;
        var minutes = int.TryParse(_config["Auth:OtpMinutes"], out var m) && m > 0 ? m : 10;
        vendor.EmailOtpExpiresAt = DateTimeOffset.UtcNow.AddMinutes(minutes);
        vendor.EmailOtpAttempts = 0;

        await _vendors.AddAsync(vendor, ct);
        await _vendors.SaveAsync(ct);

        try
        {
            await _email.SendAsync(vendor.Email,
                "Verify your InstaSafe email",
                $"<p>Hi {vendor.FirstName},</p><p>Your verification code is <b>{code}</b>. Valid for {minutes} minutes.</p>",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signup verification email failed for {Email}", vendor.Email);
        }

        return Result<VendorDto>.Success(_mapper.Map<VendorDto>(vendor));
    }
}
