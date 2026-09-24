using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Commands.UpdateVendorProfile;

public class UpdateVendorProfileCommandHandler : IRequestHandler<UpdateVendorProfileCommand, Result<VendorDto>>
{
    private readonly IVendorRepository _vendors;
    private readonly ISanitizer _sanitizer;
    private readonly IMapper _mapper;

    public UpdateVendorProfileCommandHandler(IVendorRepository vendors, ISanitizer sanitizer, IMapper mapper)
    {
        _vendors = vendors; _sanitizer = sanitizer; _mapper = mapper;
    }

    public async Task<Result<VendorDto>> Handle(UpdateVendorProfileCommand req, CancellationToken ct)
    {
        var vendor = await _vendors.GetByIdAsync(req.VendorId, ct);
        if (vendor is null)
            return Result<VendorDto>.Failure("Vendor not found.");

        vendor.DisplayName = _sanitizer.Clean(req.DisplayName, 120);
        vendor.Touch();
        await _vendors.SaveAsync(ct);

        return Result<VendorDto>.Success(_mapper.Map<VendorDto>(vendor));
    }
}
