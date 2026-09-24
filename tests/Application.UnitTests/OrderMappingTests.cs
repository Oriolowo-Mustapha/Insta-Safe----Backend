using AutoMapper;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class OrderMappingTests
{
    private readonly IMapper _mapper;

    public OrderMappingTests()
    {
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<OrderMappingProfile>(),
            NullLoggerFactory.Instance);
        config.AssertConfigurationIsValid();
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void MapsOrderToDto_IncludingItems()
    {
        var order = new Order
        {
            VendorPhone = "08012345678",
            CustomerName = "Chidi",
            CustomerPhone = "08087654321",
            DeliveryAddress = "Lekki",
            Items = new List<OrderItem>
            {
                new() { Description = "Sneakers", Quantity = 2, UnitPriceKobo = 2250000 }
            },
            AmountKobo = 4500000,
            Currency = "NGN",
            Status = OrderStatus.Held,
            PaystackReference = "ref-123",
            PaystackAuthUrl = "https://paystack.test/pay/ref-123",
            HeldAt = DateTimeOffset.UtcNow
        };

        var dto = _mapper.Map<OrderDto>(order);

        Assert.Equal(order.Id, dto.Id);
        Assert.Equal("08012345678", dto.VendorPhone);
        Assert.Equal(OrderStatus.Held, dto.Status);
        Assert.Equal(4500000, dto.AmountKobo);
        Assert.Single(dto.Items);
        Assert.Equal("Sneakers", dto.Items[0].Description);
        Assert.Equal(2, dto.Items[0].Quantity);
        Assert.Equal("ref-123", dto.PaystackReference);
    }

    [Fact]
    public void MapsOrderListToDtoList()
    {
        var orders = new List<Order> { new() { VendorPhone = "0801" }, new() { VendorPhone = "0802" } };

        var dtos = _mapper.Map<List<OrderDto>>(orders);

        Assert.Equal(2, dtos.Count);
    }
}
