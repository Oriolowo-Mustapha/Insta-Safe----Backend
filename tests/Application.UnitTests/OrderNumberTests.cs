using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Features.Orders.Queries.GetOrderByReference;
using InstaSafe.Domain.Entities;
using System.Text.RegularExpressions;

namespace Application.UnitTests;

public class OrderNumberTests
{
    [Fact]
    public void Generate_MatchesFormat_AndCleanAlphabet()
    {
        for (var i = 0; i < 200; i++)
        {
            var number = OrderNumberGenerator.Generate();
            Assert.Matches(@"^IS-[A-Z2-9]{6}$", number);
            Assert.DoesNotContain("0", number[3..]);
            Assert.DoesNotContain("1", number[3..]);
        }
    }

    [Fact]
    public void Generate_UniqueOverBatch()
    {
        var set = Enumerable.Range(0, 2000).Select(_ => OrderNumberGenerator.Generate()).ToHashSet();
        Assert.Equal(2000, set.Count);
    }

    private sealed class FakeOrders : IOrderRepository
    {
        public List<Order> Orders { get; } = new();
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Orders.FirstOrDefault(o => o.Id == id));
        public Task<Order?> GetByPaystackRefAsync(string reference, CancellationToken ct)
            => Task.FromResult(Orders.FirstOrDefault(o => o.PaystackReference == reference));
        public Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct)
            => Task.FromResult(Orders.FirstOrDefault(o => o.OrderNumber == orderNumber));
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
            => Task.FromResult(new List<Order>());
    }

    private static Order Order(string number, string paystackRef) => new()
    {
        OrderNumber = number,
        VendorPhone = "0801",
        CustomerName = "Chidi",
        CustomerPhone = "0802",
        DeliveryAddress = "Lekki",
        AmountKobo = 1000,
        PaystackReference = paystackRef
    };

    [Fact]
    public async Task Lookup_PrefersOrderNumber_OverPaystackRef()
    {
        var orders = new FakeOrders();
        orders.Orders.Add(Order("IS-AAAAAA", "ref-x"));
        var handler = new GetOrderByReferenceQueryHandler(orders,
            new AutoMapper.MapperConfiguration(
                cfg => cfg.AddProfile<InstaSafe.Application.Mapping.OrderMappingProfile>(),
                Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper());

        var byNumber = await handler.Handle(new GetOrderByReferenceQuery("is-aaaaaa"), CancellationToken.None);
        Assert.True(byNumber.IsSuccess);
        Assert.Equal("IS-AAAAAA", byNumber.Value!.OrderNumber);

        var byPaystack = await handler.Handle(new GetOrderByReferenceQuery("ref-x"), CancellationToken.None);
        Assert.True(byPaystack.IsSuccess);
        Assert.Equal("IS-AAAAAA", byPaystack.Value!.OrderNumber);
    }
}
