using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Features.Orders.Queries.GetOrderTimeline;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;

namespace Application.UnitTests;

public class OrderTimelineTests
{
    private sealed class FakeOrders : IOrderRepository
    {
        public List<Order> Orders { get; } = new();
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Orders.FirstOrDefault(o => o.Id == id));
        public Task<Order?> GetByPaystackRefAsync(string reference, CancellationToken ct)
            => Task.FromResult(Orders.FirstOrDefault(o => o.PaystackReference == reference));
        public Task AddAsync(Order order, CancellationToken ct)
        {
            Orders.Add(order);
            return Task.CompletedTask;
        }
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<List<Order>> ListAsync(int page, int pageSize, CancellationToken ct)
            => Task.FromResult(Orders.ToList());
        public Task<List<Order>> ListByVendorAsync(Guid vendorId, string vendorPhone, int page, int pageSize, CancellationToken ct)
            => Task.FromResult(new List<Order>());
        public Task<List<Order>> ListByDriverAsync(Guid driverId, string driverPhone, int page, int pageSize, CancellationToken ct)
            => Task.FromResult(new List<Order>());
        public Task<List<Order>> ListUnpaidByEmailAsync(string email, CancellationToken ct)
            => Task.FromResult(Orders.Where(o => o.BuyerEmail == email).ToList());
    }

    private static Order HeldOrder() => new()
    {
        VendorPhone = "0801",
        CustomerName = "Chidi",
        CustomerPhone = "0802",
        DeliveryAddress = "Lekki",
        AmountKobo = 4500000,
        Status = OrderStatus.Held,
        PaystackReference = "ref-timeline",
        HeldAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task Timeline_HeldOrder_ShowsCreatedAndHeld()
    {
        var orders = new FakeOrders();
        orders.Orders.Add(HeldOrder());
        var handler = new GetOrderTimelineQueryHandler(orders);

        var result = await handler.Handle(new GetOrderTimelineQuery("ref-timeline"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Held", result.Value!.Status);
        Assert.Equal("ref-timeline", result.Value.Reference);
        var keys = result.Value.Events.Select(e => e.Key).ToList();
        Assert.Equal(new[] { "created", "payment_pending", "funds_held" }, keys);
        Assert.All(result.Value.Events, e => Assert.False(string.IsNullOrWhiteSpace(e.Label)));
    }

    [Fact]
    public async Task Timeline_DeliveredOrder_ShowsWindow()
    {
        var orders = new FakeOrders();
        var order = HeldOrder();
        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = DateTimeOffset.UtcNow;
        order.ReleaseDueAt = DateTimeOffset.UtcNow.AddHours(24);
        orders.Orders.Add(order);
        var handler = new GetOrderTimelineQueryHandler(orders);

        var result = await handler.Handle(new GetOrderTimelineQuery("ref-timeline"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value!.Events, e => e.Key == "delivered");
    }

    [Fact]
    public async Task Timeline_UnknownReference_Fails()
    {
        var handler = new GetOrderTimelineQueryHandler(new FakeOrders());

        var result = await handler.Handle(new GetOrderTimelineQuery("nope"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
