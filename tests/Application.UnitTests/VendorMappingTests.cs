using AutoMapper;
using InstaSafe.Application.Features.Vendors.DTOs;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class VendorMappingTests
{
    private readonly IMapper _mapper;

    public VendorMappingTests()
    {
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<VendorMappingProfile>(),
            NullLoggerFactory.Instance);
        config.AssertConfigurationIsValid();
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void MapsVendorToDto()
    {
        var vendor = new Vendor
        {
            Phone = "08012345678",
            DisplayName = "Ada Boutique",
            AccountNumber = "0123456789",
            BankCode = "058",
            PaystackRecipientCode = "RCP_123",
            IsActive = true
        };

        var dto = _mapper.Map<VendorDto>(vendor);

        Assert.Equal(vendor.Id, dto.Id);
        Assert.Equal("08012345678", dto.Phone);
        Assert.Equal("Ada Boutique", dto.DisplayName);
        Assert.Equal("0123456789", dto.AccountNumber);
        Assert.Equal("058", dto.BankCode);
        Assert.Equal("RCP_123", dto.PaystackRecipientCode);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public void MapsVendorListToDtoList()
    {
        var vendors = new List<Vendor>
        {
            new() { Phone = "0801", DisplayName = "A" },
            new() { Phone = "0802", DisplayName = "B" }
        };

        var dtos = _mapper.Map<List<VendorDto>>(vendors);

        Assert.Equal(2, dtos.Count);
    }
}
