using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Commands.ReactivateVendor;

public class ReactivateVendorCommandHandler : IRequestHandler<ReactivateVendorCommand, Result<VendorDto>>
{
    private readonly IVendorRepository _vendors;
    private readonly IMapper _mapper;

    public ReactivateVendorCommandHandler(IVendorRepository vendors, IMapper mapper)
    {
        _vendors = vendors; _mapper = mapper;
    }

    public async Task<Result<VendorDto>> Handle(ReactivateVendorCommand req, CancellationToken ct)
    {
        var vendor = await _vendors.GetByIdAsync(req.VendorId, ct);
        if (vendor is null)
            return Result<VendorDto>.Failure("Vendor not found.");

        vendor.IsActive = true;
        vendor.Touch();
        await _vendors.SaveAsync(ct);

        return Result<VendorDto>.Success(_mapper.Map<VendorDto>(vendor));
    }
}
