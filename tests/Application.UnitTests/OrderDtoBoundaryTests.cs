using System.Text.Json;
using AutoMapper;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

/// <summary>
/// The anonymous track endpoints and the rider portal used to return the full
/// OrderDto, which leaked buyer PII and Paystack internals to anyone holding an
/// order number. These tests pin the new boundary so it cannot be widened by
/// accident: exact allowed field sets, plus a wire-level check that no
/// sensitive value survives serialization.
/// </summary>
public class OrderDtoBoundaryTests
{
    private static readonly IMapper Mapper = new MapperConfiguration(
        cfg => cfg.AddProfile<OrderMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private static Order LoadedOrder() => new()
    {
        OrderNumber = "IS-8K4N2Q",
        VendorPhone = "08011111111",
        CustomerName = "Chidi",
        CustomerPhone = "08022222222",
        BuyerEmail = "buyer@example.com",
        DeliveryAddress = "Lekki Phase 1",
        AmountKobo = 4500000,
        DeliveryFeeKobo = 500000,
        Currency = "NGN",
        Status = OrderStatus.Held,
        PaystackReference = "PS_REF_SENTINEL",
        PaystackAuthUrl = "https://pay.test/AUTH_SENTINEL",
        TransferReference = "TRF_SENTINEL",
        RefundReference = "RFND_SENTINEL",
        PayVirtualAccountNumber = "9998887776",
        PayVirtualAccountBank = "Wema",
        DriverPhone = "08055556666",
        DriverTransferReference = "DRV_TRF_SENTINEL",
        Fulfillment = FulfillmentType.Dispatch,
        Items = { new OrderItem { Description = "Sneakers", Quantity = 2, UnitPriceKobo = 2250000 } }
    };

    private static string Wire<T>(T dto) => JsonSerializer.Serialize(dto);

    [Fact]
    public void MappingConfiguration_IsValidForEveryOrderShape()
    {
        new MapperConfiguration(cfg => cfg.AddProfile<OrderMappingProfile>(), NullLoggerFactory.Instance)
            .AssertConfigurationIsValid();
    }

    [Fact]
    public void PublicOrderDto_ExposesOnlyTheAgreedSurface()
    {
        var names = typeof(PublicOrderDto).GetProperties().Select(p => p.Name).OrderBy(n => n);
        Assert.Equal(
            new[]
            {
                "AmountKobo", "Currency", "CustomerName", "DeliveredAt", "DeliveryAddress",
                "DeliveryFeeKobo", "DisputeReason", "Fulfillment", "HeldAt", "Id", "Items",
                "OrderNumber", "ReleaseDueAt", "ReleasedAt", "Status"
            }.OrderBy(n => n),
            names);
    }

    [Fact]
    public void DispatchOrderDto_ExposesOnlyTheAgreedSurface()
    {
        var names = typeof(DispatchOrderDto).GetProperties().Select(p => p.Name).OrderBy(n => n);
        Assert.Equal(
            new[]
            {
                "Currency", "CustomerName", "CustomerPhone", "DeliveredAt", "DeliveryAddress",
                "DeliveryFeeKobo", "DriverPhone", "Fulfillment", "Id", "Items", "OrderNumber",
                "ReleaseDueAt", "Status"
            }.OrderBy(n => n),
            names);
    }

    [Fact]
    public void PublicOrderDto_CarriesNoBuyerPiiOrPaystackInternals()
    {
        var json = Wire(Mapper.Map<PublicOrderDto>(LoadedOrder()));

        foreach (var secret in new[]
        {
            "buyer@example.com",     // buyerEmail
            "08022222222",           // customerPhone
            "08011111111",           // vendorPhone
            "AUTH_SENTINEL",         // paystackAuthUrl
            "PS_REF_SENTINEL",       // paystackReference
            "TRF_SENTINEL",          // transferReference
            "RFND_SENTINEL",         // refundReference
            "DRV_TRF_SENTINEL",      // driverTransferReference
            "9998887776",            // payVirtualAccountNumber
            "Wema"                   // payVirtualAccountBank
        })
            Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
    }

    [Fact]
    public void DispatchOrderDto_CarriesNoPaystackInternalsOrOrderValue()
    {
        var json = Wire(Mapper.Map<DispatchOrderDto>(LoadedOrder()));

        foreach (var secret in new[]
        {
            "buyer@example.com", "AUTH_SENTINEL", "PS_REF_SENTINEL", "TRF_SENTINEL",
            "RFND_SENTINEL", "DRV_TRF_SENTINEL", "9998887776", "Wema",
            "08011111111", "4500000"
        })
            Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicOrderDto_StillCarriesWhatTheTrackPageNeeds()
    {
        var dto = Mapper.Map<PublicOrderDto>(LoadedOrder());

        Assert.Equal("IS-8K4N2Q", dto.OrderNumber);
        Assert.Equal(OrderStatus.Held, dto.Status);
        Assert.Equal(4500000, dto.AmountKobo);
        Assert.Equal("Chidi", dto.CustomerName);
        Assert.Equal("Lekki Phase 1", dto.DeliveryAddress);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("Sneakers", Assert.Single(dto.Items).Description);
    }

    [Fact]
    public void DispatchOrderDto_StillCarriesWhatTheRiderNeeds()
    {
        var dto = Mapper.Map<DispatchOrderDto>(LoadedOrder());

        Assert.Equal("IS-8K4N2Q", dto.OrderNumber);
        Assert.Equal("Chidi", dto.CustomerName);
        Assert.Equal("08022222222", dto.CustomerPhone);
        Assert.Equal("Lekki Phase 1", dto.DeliveryAddress);
        Assert.Equal(500000, dto.DeliveryFeeKobo);
        Assert.Equal("08055556666", dto.DriverPhone);
    }

    [Fact]
    public void OrderDto_StillServesTheAdminConsoleUnchanged()
    {
        var dto = Mapper.Map<OrderDto>(LoadedOrder());

        Assert.Equal("buyer@example.com", dto.BuyerEmail);
        Assert.Equal("9998887776", dto.PayVirtualAccountNumber);
        Assert.Equal("PS_REF_SENTINEL", dto.PaystackReference);
        Assert.Equal(4500000, dto.AmountKobo);
    }
}
