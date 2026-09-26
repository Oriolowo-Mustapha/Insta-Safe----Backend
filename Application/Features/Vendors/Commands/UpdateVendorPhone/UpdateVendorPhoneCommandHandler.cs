using AutoMapper;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Commands.UpdateVendorPhone;

public class UpdateVendorPhoneCommandHandler : IRequestHandler<UpdateVendorPhoneCommand, Result<VendorDto>>
{
    private readonly IVendorRepository _vendors;
    private readonly ISanitizer _sanitizer;
    private readonly IMapper _mapper;

    public UpdateVendorPhoneCommandHandler(IVendorRepository vendors, ISanitizer sanitizer, IMapper mapper)
    {
        _vendors = vendors; _sanitizer = sanitizer; _mapper = mapper;
    }

    public async Task<Result<VendorDto>> Handle(UpdateVendorPhoneCommand req, CancellationToken ct)
    {
        var vendor = await _vendors.GetByIdAsync(req.VendorId, ct);
        if (vendor is null)
            return Result<VendorDto>.Failure("Vendor not found.");

        var phone = PhoneNormalizer.Normalize(req.Phone);
        if (await _vendors.ExistsByPhoneAsync(phone, ct) && vendor.Phone != phone)
            return Result<VendorDto>.Failure($"Phone '{phone}' is already in use.");

        vendor.Phone = _sanitizer.Clean(phone, 20);
        vendor.Touch();
        await _vendors.SaveAsync(ct);

        return Result<VendorDto>.Success(_mapper.Map<VendorDto>(vendor));
    }
}
