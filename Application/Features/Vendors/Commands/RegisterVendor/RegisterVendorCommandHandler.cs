using AutoMapper;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using InstaSafe.Domain.Entities;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Commands.RegisterVendor;

public class RegisterVendorCommandHandler : IRequestHandler<RegisterVendorCommand, Result<VendorDto>>
{
    private readonly IVendorRepository _vendors;
    private readonly IPaystackClient _paystack;
    private readonly ISanitizer _sanitizer;
    private readonly IMapper _mapper;

    public RegisterVendorCommandHandler(
        IVendorRepository vendors, IPaystackClient paystack, ISanitizer sanitizer, IMapper mapper)
    {
        _vendors = vendors; _paystack = paystack; _sanitizer = sanitizer; _mapper = mapper;
    }

    public async Task<Result<VendorDto>> Handle(RegisterVendorCommand req, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(req.Phone);
        if (string.IsNullOrWhiteSpace(phone))
            return Result<VendorDto>.Failure("Vendor phone is required.");

        if (await _vendors.ExistsByPhoneAsync(phone, ct))
            return Result<VendorDto>.Failure($"Vendor with phone '{phone}' already exists.");

        var vendor = new Vendor
        {
            Phone = _sanitizer.Clean(phone, 20),
            DisplayName = _sanitizer.Clean(req.DisplayName, 120),
            FirstName = req.FirstName is null ? null : _sanitizer.Clean(req.FirstName, 120),
            LastName = req.LastName is null ? null : _sanitizer.Clean(req.LastName, 120),
            Email = req.Email is null ? null : _sanitizer.Clean(req.Email.Trim(), 200),
            AccountNumber = req.AccountNumber is null ? null : _sanitizer.Clean(req.AccountNumber, 20),
            BankCode = req.BankCode is null ? null : _sanitizer.Clean(req.BankCode, 10),
            IsActive = true
        };

        if (vendor.AccountNumber is not null && vendor.BankCode is not null)
        {
            var recipient = await _paystack.CreateRecipientAsync(
                vendor.AccountNumber, vendor.BankCode, vendor.DisplayName, ct);
            if (recipient is not null) vendor.PaystackRecipientCode = recipient;
        }

        await _vendors.AddAsync(vendor, ct);
        await _vendors.SaveAsync(ct);

        return Result<VendorDto>.Success(_mapper.Map<VendorDto>(vendor));
    }
}
