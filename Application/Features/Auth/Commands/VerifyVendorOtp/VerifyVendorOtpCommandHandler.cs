using AutoMapper;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Auth.Commands.RequestVendorOtp;
using InstaSafe.Application.Features.Vendors.DTOs;
using InstaSafe.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace InstaSafe.Application.Features.Auth.Commands.VerifyVendorOtp;

public class VerifyVendorOtpCommandHandler : IRequestHandler<VerifyVendorOtpCommand, Result<VendorAuthResponse>>
{
    private readonly IVendorRepository _vendors;
    private readonly IOtpService _otp;
    private readonly IJwtTokenService _tokens;
    private readonly IMapper _mapper;
    private readonly IConfiguration _config;

    public VerifyVendorOtpCommandHandler(
        IVendorRepository vendors, IOtpService otp, IJwtTokenService tokens,
        IMapper mapper, IConfiguration config)
    {
        _vendors = vendors; _otp = otp; _tokens = tokens; _mapper = mapper; _config = config;
    }

    public async Task<Result<VendorAuthResponse>> Handle(VerifyVendorOtpCommand req, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(req.Phone);
        var vendor = await _vendors.GetByPhoneAsync(phone, ct);
        if (vendor is null || !vendor.IsActive)
            return Result<VendorAuthResponse>.Failure("No active vendor found for this phone.");
        if (vendor.OtpHash is null || !vendor.OtpHash.Contains('.'))
            return Result<VendorAuthResponse>.Failure("No login code requested. Request one first.");
        if (vendor.OtpExpiresAt is null || DateTimeOffset.UtcNow > vendor.OtpExpiresAt)
            throw new DomainValidationException("Login code has expired. Request a new one.");
        if (vendor.OtpAttempts >= 5)
            throw new ForbiddenAccessException("Too many attempts. Request a new code.");

        var salt = vendor.OtpHash.Split('.', 2)[1];
        if (vendor.OtpHash != _otp.Hash(req.Code.Trim(), salt) + "." + salt)
        {
            vendor.OtpAttempts++;
            vendor.Touch();
            await _vendors.SaveAsync(ct);
            throw new DomainValidationException("Invalid login code.");
        }

        vendor.OtpHash = null;
        vendor.OtpExpiresAt = null;
        vendor.OtpAttempts = 0;
        vendor.Touch();
        await _vendors.SaveAsync(ct);

        var hours = double.TryParse(_config["Auth:TokenHours"], out var h) && h > 0 ? h : 24;
        var token = _tokens.CreateVendorToken(vendor.Id, vendor.Phone);
        return Result<VendorAuthResponse>.Success(
            new VendorAuthResponse(token, _mapper.Map<VendorDto>(vendor), hours));
    }
}
