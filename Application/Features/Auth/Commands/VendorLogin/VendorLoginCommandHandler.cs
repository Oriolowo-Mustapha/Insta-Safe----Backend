using AutoMapper;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Auth.Commands.RequestVendorOtp;
using InstaSafe.Application.Features.Vendors.DTOs;
using InstaSafe.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace InstaSafe.Application.Features.Auth.Commands.VendorLogin;

public class VendorLoginCommandHandler : IRequestHandler<VendorLoginCommand, Result<VendorAuthResponse>>
{
    private readonly IVendorRepository _vendors;
    private readonly IPasswordHasher _passwords;
    private readonly IJwtTokenService _tokens;
    private readonly IMapper _mapper;
    private readonly IConfiguration _config;

    public VendorLoginCommandHandler(
        IVendorRepository vendors, IPasswordHasher passwords, IJwtTokenService tokens,
        IMapper mapper, IConfiguration config)
    {
        _vendors = vendors; _passwords = passwords; _tokens = tokens; _mapper = mapper; _config = config;
    }

    public async Task<Result<VendorAuthResponse>> Handle(VendorLoginCommand req, CancellationToken ct)
    {
        var id = req.LoginId.Trim();
        Domain.Entities.Vendor? vendor = id.Contains('@')
            ? await _vendors.GetByEmailAsync(id.ToLowerInvariant(), ct)
            : await _vendors.GetByPhoneAsync(PhoneNormalizer.Normalize(id), ct);

        if (vendor is null || !vendor.IsActive)
            return Result<VendorAuthResponse>.Failure("Invalid login details.");
        if (!_passwords.Verify(req.Password, vendor.PasswordHash))
            return Result<VendorAuthResponse>.Failure("Invalid login details.");

        var hours = double.TryParse(_config["Auth:TokenHours"], out var h) && h > 0 ? h : 24;
        var token = _tokens.CreateVendorToken(vendor.Id, vendor.Phone);
        return Result<VendorAuthResponse>.Success(
            new VendorAuthResponse(token, _mapper.Map<VendorDto>(vendor), hours));
    }
}
