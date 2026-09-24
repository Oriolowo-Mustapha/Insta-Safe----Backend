using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Queries.ListVendors;

public class ListVendorsQueryHandler : IRequestHandler<ListVendorsQuery, Result<List<VendorDto>>>
{
    private readonly IVendorRepository _vendors;
    private readonly IMapper _mapper;

    public ListVendorsQueryHandler(IVendorRepository vendors, IMapper mapper)
    {
        _vendors = vendors; _mapper = mapper;
    }

    public async Task<Result<List<VendorDto>>> Handle(ListVendorsQuery req, CancellationToken ct)
    {
        var page = req.Page <= 0 ? 1 : req.Page;
        var size = Math.Clamp(req.PageSize, 1, 100);
        var vendors = await _vendors.ListAsync(page, size, req.ActiveOnly, ct);
        return Result<List<VendorDto>>.Success(_mapper.Map<List<VendorDto>>(vendors));
    }
}
