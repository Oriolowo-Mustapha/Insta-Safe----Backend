using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Queries.GetVendorById;

public class GetVendorByIdQueryHandler : IRequestHandler<GetVendorByIdQuery, Result<VendorDto>>
{
    private readonly IVendorRepository _vendors;
    private readonly IMapper _mapper;

    public GetVendorByIdQueryHandler(IVendorRepository vendors, IMapper mapper)
    {
        _vendors = vendors; _mapper = mapper;
    }

    public async Task<Result<VendorDto>> Handle(GetVendorByIdQuery req, CancellationToken ct)
    {
        var vendor = await _vendors.GetByIdAsync(req.VendorId, ct);
        if (vendor is null)
            return Result<VendorDto>.Failure("Vendor not found.");
        return Result<VendorDto>.Success(_mapper.Map<VendorDto>(vendor));
    }
}
