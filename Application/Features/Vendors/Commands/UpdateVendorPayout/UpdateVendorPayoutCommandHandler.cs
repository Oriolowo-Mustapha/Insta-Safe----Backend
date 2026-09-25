using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Commands.UpdateVendorPayout;

public class UpdateVendorPayoutCommandHandler : IRequestHandler<UpdateVendorPayoutCommand, Result<VendorDto>>
{
    private readonly IVendorRepository _vendors;
    private readonly IPaystackClient _paystack;
    private readonly ISanitizer _sanitizer;
    private readonly IMapper _mapper;

    public UpdateVendorPayoutCommandHandler(
        IVendorRepository vendors, IPaystackClient paystack, ISanitizer sanitizer, IMapper mapper)
    {
        _vendors = vendors; _paystack = paystack; _sanitizer = sanitizer; _mapper = mapper;
    }

    public async Task<Result<VendorDto>> Handle(UpdateVendorPayoutCommand req, CancellationToken ct)
    {
        var vendor = await _vendors.GetByIdAsync(req.VendorId, ct);
        if (vendor is null)
            return Result<VendorDto>.Failure("Vendor not found.");

        vendor.AccountNumber = _sanitizer.Clean(req.AccountNumber, 20);
        vendor.BankCode = _sanitizer.Clean(req.BankCode, 10);

        var recipient = await _paystack.CreateRecipientAsync(
            vendor.AccountNumber, vendor.BankCode, vendor.DisplayName, ct);
        if (recipient is not null) vendor.PaystackRecipientCode = recipient;

        vendor.OnboardingCompleted = true;
        vendor.Touch();
        await _vendors.SaveAsync(ct);

        return Result<VendorDto>.Success(_mapper.Map<VendorDto>(vendor));
    }
}
