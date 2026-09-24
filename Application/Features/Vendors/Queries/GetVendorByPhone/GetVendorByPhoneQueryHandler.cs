using AutoMapper;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Queries.GetVendorByPhone;

public class GetVendorByPhoneQueryHandler : IRequestHandler<GetVendorByPhoneQuery, Result<VendorDto>>
{
    private readonly IVendorRepository _vendors;
    private readonly IMapper _mapper;

    public GetVendorByPhoneQueryHandler(IVendorRepository vendors, IMapper mapper)
    {
        _vendors = vendors; _mapper = mapper;
    }

    public async Task<Result<VendorDto>> Handle(GetVendorByPhoneQuery req, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(req.Phone);
        var vendor = await _vendors.GetByPhoneAsync(phone, ct);
        if (vendor is null)
            return Result<VendorDto>.Failure("Vendor not found.");
        return Result<VendorDto>.Success(_mapper.Map<VendorDto>(vendor));
    }
}
