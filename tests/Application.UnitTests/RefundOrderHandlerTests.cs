using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Features.Orders.Commands.RefundOrder;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class RefundOrderHandlerTests
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
            => Task.FromResult(Orders.Where(o => o.VendorId == vendorId || o.VendorPhone == vendorPhone).ToList());
        public Task<List<Order>> ListByDriverAsync(Guid driverId, string driverPhone, int page, int pageSize, CancellationToken ct)
            => Task.FromResult(Orders.Where(o => o.DriverId == driverId || o.DriverPhone == driverPhone).ToList());
        public Task<List<Order>> ListUnpaidByEmailAsync(string email, CancellationToken ct)
            => Task.FromResult(Orders.Where(o => o.BuyerEmail == email).ToList());
    }

    private sealed class NullSender : IWhatsAppSender
    {
        public Task SendTextAsync(string toPhone, string body, CancellationToken ct) => Task.CompletedTask;
        public Task SendTemplateAsync(string toPhone, string templateName, Dictionary<string, string> vars, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class NullEmail : IEmailSender
    {
        public bool IsConfigured => false;
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NullVendors : IVendorRepository
    {
        public Task AddAsync(Vendor vendor, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct) => Task.FromResult(false);
        public Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Vendor?>(null);
        public Task<Vendor?> GetByPhoneAsync(string phone, CancellationToken ct) => Task.FromResult<Vendor?>(null);
        public Task<Vendor?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<Vendor?>(null);
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct) => Task.FromResult(false);
        public Task<List<Vendor>> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct)
            => Task.FromResult(new List<Vendor>());
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePaystack : IPaystackClient
    {
        public bool RefundSucceeds { get; set; } = true;
        public int RefundCalls { get; private set; }
        public Task<(string Reference, string AuthUrl)> InitializeTransactionAsync(
            string email, long amountKobo, Guid orderId, CancellationToken ct)
            => Task.FromResult(("ref", "https://pay.test"));
        public Task<bool> VerifyTransactionAsync(string reference, CancellationToken ct)
            => Task.FromResult(true);
        public Task<string?> CreateRecipientAsync(string accountNumber, string bankCode, string name, CancellationToken ct)
            => Task.FromResult<string?>("RCP_TEST");
        public Task<string?> InitiateTransferAsync(long amountKobo, string recipientCode, string reason, CancellationToken ct)
            => Task.FromResult<string?>("TRF_TEST");
        public Task<(bool Success, string? RefundReference, string? Error)> RefundTransactionAsync(string reference, CancellationToken ct)
        {
            RefundCalls++;
            return Task.FromResult(RefundSucceeds
                ? (true, "RFND_TEST", (string?)null)
                : (false, (string?)null, "Paystack refund rejected (400)."));
        }
        public Task<(bool Success, string? CustomerCode, string? Error)> CreateCustomerAsync(string email, string firstName, string lastName, string phone, System.Guid orderId, CancellationToken ct)
            => Task.FromResult((true, (string?)"CUS_TEST", (string?)null));
        public Task<(bool Success, string? AccountNumber, string? AccountName, string? Bank, string? Error)> AssignDedicatedAccountAsync(string customerCode, string? preferredBank, CancellationToken ct)
            => Task.FromResult((true, (string?)"0123456789", (string?)"IN Heap Ogbonna", (string?)"Wema", (string?)null));
        public Task<System.Collections.Generic.List<(string Name, string Slug, string Code)>> ListTransferBanksAsync(CancellationToken ct)
            => Task.FromResult(new System.Collections.Generic.List<(string Name, string Slug, string Code)>());
    }

    private static (RefundOrderCommandHandler Handler, FakeOrders Orders, FakePaystack Paystack) Create()
    {
        var orders = new FakeOrders();
        var paystack = new FakePaystack();
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<OrderMappingProfile>(),
            NullLoggerFactory.Instance);
        var notifier = new InstaSafe.Application.Common.Notifications.OrderNotifier(
            new NullSender(), new NullEmail(), new NullVendors(),
            NullLogger<InstaSafe.Application.Common.Notifications.OrderNotifier>.Instance);
        return (new RefundOrderCommandHandler(orders, paystack, notifier, config.CreateMapper()), orders, paystack);
    }

    private static Order HeldOrder() => new()
    {
        VendorPhone = "0801",
        CustomerName = "Chidi",
        CustomerPhone = "0802",
        DeliveryAddress = "Lekki",
        AmountKobo = 4500000,
        Status = OrderStatus.Held,
        PaystackReference = "ref-held"
    };

    [Fact]
    public async Task Refund_HeldOrder_CallsPaystack_AndStoresReference()
    {
        var (handler, orders, paystack) = Create();
        var order = HeldOrder();
        orders.Orders.Add(order);

        var result = await handler.Handle(new RefundOrderCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal("RFND_TEST", order.RefundReference);
        Assert.Equal(1, paystack.RefundCalls);
        Assert.Equal("RFND_TEST", result.Value!.RefundReference);
    }

    [Fact]
    public async Task Refund_HeldOrder_PaystackFailure_KeepsHeld()
    {
        var (handler, orders, _) = Create();
        var order = HeldOrder();
        orders.Orders.Add(order);
        var paystack = new FakePaystack { RefundSucceeds = false };
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<OrderMappingProfile>(),
            NullLoggerFactory.Instance);
        var notifier = new InstaSafe.Application.Common.Notifications.OrderNotifier(
            new NullSender(), new NullEmail(), new NullVendors(),
            NullLogger<InstaSafe.Application.Common.Notifications.OrderNotifier>.Instance);
        handler = new RefundOrderCommandHandler(orders, paystack, notifier, config.CreateMapper());

        var result = await handler.Handle(new RefundOrderCommand(order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderStatus.Held, order.Status);
        Assert.Null(order.RefundReference);
    }

    [Fact]
    public async Task Refund_AwaitingPayment_MarksRefunded_WithoutPaystackCall()
    {
        var (handler, orders, paystack) = Create();
        var order = HeldOrder();
        order.Status = OrderStatus.AwaitingPayment;
        order.PaystackReference = null;
        orders.Orders.Add(order);

        var result = await handler.Handle(new RefundOrderCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(0, paystack.RefundCalls);
    }

    [Fact]
    public async Task Refund_ReleasedOrder_ThrowsConflict()
    {
        var (handler, orders, _) = Create();
        var order = HeldOrder();
        order.Status = OrderStatus.Released;
        orders.Orders.Add(order);

        await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(new RefundOrderCommand(order.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Refund_MissingOrder_Fails()
    {
        var (handler, _, _) = Create();

        var result = await handler.Handle(new RefundOrderCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
