using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using InstaSafe.Domain.Exceptions;
using MediatR;

namespace InstaSafe.Application.Features.Auth.Commands.VerifyEmailCode;

public class VerifyEmailCodeCommandHandler : IRequestHandler<VerifyEmailCodeCommand, Result<VendorDto>>
{
    private readonly IVendorRepository _vendors;
    private readonly IOtpService _otp;
    private readonly IMapper _mapper;

    public VerifyEmailCodeCommandHandler(IVendorRepository vendors, IOtpService otp, IMapper mapper)
    {
        _vendors = vendors; _otp = otp; _mapper = mapper;
    }

    public async Task<Result<VendorDto>> Handle(VerifyEmailCodeCommand req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var vendor = await _vendors.GetByEmailAsync(email, ct);
        if (vendor is null || !vendor.IsActive)
            return Result<VendorDto>.Failure("No active vendor found for this email.");
        if (vendor.EmailVerified)
            return Result<VendorDto>.Success(_mapper.Map<VendorDto>(vendor));
        if (vendor.EmailOtpHash is null || !vendor.EmailOtpHash.Contains('.'))
            return Result<VendorDto>.Failure("No verification code requested. Request one first.");
        if (vendor.EmailOtpExpiresAt is null || DateTimeOffset.UtcNow > vendor.EmailOtpExpiresAt)
            throw new DomainValidationException("Verification code has expired. Request a new one.");
        if (vendor.EmailOtpAttempts >= 5)
            throw new ForbiddenAccessException("Too many attempts. Request a new code.");

        var salt = vendor.EmailOtpHash.Split('.', 2)[1];
        if (vendor.EmailOtpHash != _otp.Hash(req.Code.Trim(), salt) + "." + salt)
        {
            vendor.EmailOtpAttempts++;
            vendor.Touch();
            await _vendors.SaveAsync(ct);
            throw new DomainValidationException("Invalid verification code.");
        }

        vendor.EmailVerified = true;
        vendor.EmailOtpHash = null;
        vendor.EmailOtpExpiresAt = null;
        vendor.EmailOtpAttempts = 0;
        vendor.Touch();
        await _vendors.SaveAsync(ct);

        return Result<VendorDto>.Success(_mapper.Map<VendorDto>(vendor));
    }
}
