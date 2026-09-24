using AutoMapper;
using InstaSafe.Application.Features.Vendors.DTOs;
using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Mapping;

public class VendorMappingProfile : Profile
{
    public VendorMappingProfile()
    {
        CreateMap<Vendor, VendorDto>();
    }
}
