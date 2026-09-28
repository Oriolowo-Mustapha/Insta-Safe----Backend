using AutoMapper;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Notifications;
using InstaSafe.Application.Features.Dispatch.Commands.ConfirmDelivery;
using InstaSafe.Application.Features.Orders.Commands.ConfirmSatisfaction;
using InstaSafe.Application.Features.Orders.Commands.DisputeOrder;
using InstaSafe.Application.Features.Orders.Commands.ResolveDispute;
using InstaSafe.Application.Features.Orders.Commands.VerifyOtp;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Exceptions;
using InstaSafe.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class DeliveryFlowTests : IDisposable
{
    private sealed class FakeOrders : IOrderRepository
    {
        private readonly AppDbContext _db;
        public FakeOrders(AppDbContext db) => _db = db;
        public async Task AddAsync(Order order, CancellationToken ct) => await _db.Orders.AddAsync(order, ct);
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct) => _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct)!;
        public Task<Order?> GetByPaystackRefAsync(string reference, CancellationToken ct) => _db.Orders.FirstOrDefaultAsync(o => o.PaystackReference == reference, ct)!;
        public Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct)
            => _db.Orders.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct)!;
        public Task<List<Order>> ListAsync(int page, int pageSize, CancellationToken ct) => _db.Orders.ToListAsync(ct);
        public Task<List<Order>> ListByVendorAsync(Guid vendorId, string vendorPhone, int page, int pageSize, CancellationToken ct)
            => _db.Orders.Where(o => o.VendorId == vendorId || o.VendorPhone == vendorPhone).ToListAsync(ct);
        public Task<List<Order>> ListByDriverAsync(Guid driverId, string driverPhone, int page, int pageSize, CancellationToken ct)
            => _db.Orders.Where(o => o.DriverId == driverId || o.DriverPhone == driverPhone).ToListAsync(ct);
        public Task<List<Order>> ListUnpaidByEmailAsync(string email, CancellationToken ct)
            => _db.Orders.Where(o => o.BuyerEmail == email).ToListAsync(ct);
        public Task SaveAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
    }

    private sealed class FakeOtp : IOtpService
    {
        public string NextCode { get; set; } = "123456";
        public string GenerateOtp(int digits = 6) => NextCode;
        public string NewSalt() => "testsalt12345678";
        public string Hash(string otp, string salt) => $"HASH:{salt}:{otp}";
    }

    private sealed class FakePaystack : IPaystackClient
    {
        public List<(long Amount, string Recipient)> Transfers { get; } = new();
        public bool RefundSucceeds { get; set; } = true;
        public Task<(string Reference, string AuthUrl)> InitializeTransactionAsync(string email, long amountKobo, Guid orderId, CancellationToken ct)
            => Task.FromResult(("ref", "https://pay.test"));
        public Task<bool> VerifyTransactionAsync(string reference, CancellationToken ct) => Task.FromResult(true);
        public Task<string?> CreateRecipientAsync(string accountNumber, string bankCode, string name, CancellationToken ct)
            => Task.FromResult<string?>("RCP_TEST");
        public Task<string?> InitiateTransferAsync(long amountKobo, string recipientCode, string reason, CancellationToken ct)
        {
            Transfers.Add((amountKobo, recipientCode));
            return Task.FromResult<string?>("TRF_TEST");
        }
        public Task<(bool Success, string? RefundReference, string? Error)> RefundTransactionAsync(string reference, CancellationToken ct)
            => Task.FromResult(RefundSucceeds ? (true, (string?)"RFND_T", (string?)null) : (false, (string?)null, "nope"));
        public Task<(bool Success, string? CustomerCode, string? Error)> CreateCustomerAsync(string email, string firstName, string lastName, string phone, Guid orderId, CancellationToken ct)
            => Task.FromResult((true, (string?)"CUS_TEST", (string?)null));
        public Task<(bool Success, string? AccountNumber, string? AccountName, string? Bank, string? Error)> AssignDedicatedAccountAsync(string customerCode, string? preferredBank, CancellationToken ct)
            => Task.FromResult((true, (string?)"0123456789", (string?)"Ada Obi", (string?)"Wema", (string?)null));
        public Task<List<(string Name, string Slug, string Code)>> ListTransferBanksAsync(CancellationToken ct)
            => Task.FromResult(new List<(string Name, string Slug, string Code)>());
        public Task<AccountResolveResult> ResolveAccountAsync(
            string accountNumber, string bankCode, CancellationToken ct)
            => Task.FromResult(new AccountResolveResult(true, "Ada Obi", ResolveFailureKind.Invalid, ""));
        public Task<List<(string Name, string Slug, string Code)>> ListAllBanksAsync(CancellationToken ct)
            => Task.FromResult(new List<(string Name, string Slug, string Code)>());
    }

    private sealed class FakeSender : IWhatsAppSender
    {
        public List<string> Sent { get; } = new();
        public Task SendTextAsync(string toPhone, string body, CancellationToken ct)
        {
            Sent.Add(body);
            return Task.CompletedTask;
        }
        public Task SendTemplateAsync(string toPhone, string templateName, Dictionary<string, string> vars, CancellationToken ct)
            => Task.CompletedTask;
    }
    private sealed class FakeEmail : IEmailSender
    {
        public bool IsConfigured => true;
        public List<string> Sent { get; } = new();
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
        {
            Sent.Add(subject);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeVendors : IVendorRepository
    {
        public Task AddAsync(Vendor vendor, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct) => Task.FromResult(false);
        public Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Vendor?>(null);
        public Task<Vendor?> GetByPhoneAsync(string phone, CancellationToken ct) => Task.FromResult<Vendor?>(null);
        public Task<Vendor?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<Vendor?>(null);
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct) => Task.FromResult(false);
        public Task<List<Vendor>> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct) => Task.FromResult(new List<Vendor>());
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class PassSanitizer : ISanitizer
    {
        public string Clean(string? input, int maxLength = 2000)
            => string.IsNullOrEmpty(input) ? string.Empty : input.Length > maxLength ? input[..maxLength] : input;
    }

    private readonly AppDbContext _db;
    private readonly FakeOrders _orders;
    private readonly FakeOtp _otp = new();
    private readonly FakePaystack _paystack = new();
    private readonly FakeSender _wa = new();
    private readonly FakeEmail _email = new();
    private readonly IMapper _mapper;

    public DeliveryFlowTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options, new FakePublisher());
        _orders = new FakeOrders(_db);
        var config = new MapperConfiguration(
            cfg => { cfg.AddProfile<OrderMappingProfile>(); cfg.AddProfile<DispatcherMappingProfile>(); },
            NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();
    }

    public void Dispose() => _db.Dispose();

    private OrderNotifier Notifier() => new(_wa, _email, new FakeVendors(), NullLogger<OrderNotifier>.Instance);

    private static Order DispatchHeldOrder() => new()
    {
        VendorPhone = "0801",
        CustomerName = "Chidi",
        CustomerPhone = "0802",
        DeliveryAddress = "Lekki",
        AmountKobo = 5000000,
        DeliveryFeeKobo = 500000,
        Fulfillment = FulfillmentType.Dispatch,
        Status = OrderStatus.Held,
        PaystackReference = "ref-held",
        VendorRecipientCode = "RCP_VENDOR",
        DriverPhone = "0803",
        DriverRecipientCode = "RCP_DRIVER",
        OtpHash = "HASH:testsalt12345678:123456.testsalt12345678",
        OtpExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
    };

    [Fact]
    public void VendorRemainder_SubtractsPaidDriverFee()
    {
        var order = new Order { AmountKobo = 5000000, DeliveryFeeKobo = 500000, DriverTransferReference = "TRF_X" };
        Assert.Equal(4500000, OrderReleaseCalculator.VendorRemainderKobo(order));
    }

    [Fact]
    public void VendorRemainder_FullWhenDriverUnpaid()
    {
        var order = new Order { AmountKobo = 5000000, DeliveryFeeKobo = 500000 };
        Assert.Equal(5000000, OrderReleaseCalculator.VendorRemainderKobo(order));
    }

    [Fact]
    public async Task ConfirmDelivery_PaysDriverFee_AndSetsWindow()
    {
        var order = DispatchHeldOrder();
        await _orders.AddAsync(order, CancellationToken.None);
        await _orders.SaveAsync(CancellationToken.None);
        var handler = new ConfirmDeliveryCommandHandler(
            _orders, _db, _otp, _paystack, Notifier(), _mapper);

        var result = await handler.Handle(
            new ConfirmDeliveryCommand(Guid.NewGuid(), "0803", order.Id, "123456"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.NotNull(order.DeliveredAt);
        Assert.NotNull(order.ReleaseDueAt);
        Assert.Equal("TRF_TEST", order.DriverTransferReference);
        Assert.Single(_paystack.Transfers);
        Assert.Equal(500000, _paystack.Transfers[0].Amount);
        Assert.Contains(_wa.Sent, m => m.Contains("DELIVERED"));
    }

    [Fact]
    public async Task ConfirmDelivery_WrongDriver_Rejected()
    {
        var order = DispatchHeldOrder();
        await _orders.AddAsync(order, CancellationToken.None);
        await _orders.SaveAsync(CancellationToken.None);
        var handler = new ConfirmDeliveryCommandHandler(
            _orders, _db, _otp, _paystack, Notifier(), _mapper);

        var result = await handler.Handle(
            new ConfirmDeliveryCommand(Guid.NewGuid(), "08039999999", order.Id, "123456"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderStatus.Held, order.Status);
    }

    [Fact]
    public async Task Dispute_FreezesOrder_AndResolveRelease_PaysRemainder()
    {
        var order = DispatchHeldOrder();
        order.DriverTransferReference = "TRF_DRIVER";
        order.Status = OrderStatus.Delivered;
        await _orders.AddAsync(order, CancellationToken.None);
        await _orders.SaveAsync(CancellationToken.None);

        var dispute = new DisputeOrderCommandHandler(
            _orders, new PassSanitizer(), Notifier(), _mapper);
        var disputed = await dispute.Handle(new DisputeOrderCommand(order.Id, "Item broken"), CancellationToken.None);
        Assert.True(disputed.IsSuccess);
        Assert.Equal(OrderStatus.Disputed, order.Status);

        var resolve = new ResolveDisputeCommandHandler(
            _orders, _db, _paystack, Notifier(), _mapper);
        var released = await resolve.Handle(
            new ResolveDisputeCommand(order.Id, DisputeResolution.Release), CancellationToken.None);

        Assert.True(released.IsSuccess);
        Assert.Equal(OrderStatus.Released, order.Status);
        Assert.Equal("TRF_TEST", order.TransferReference);
        Assert.Equal(4500000, _paystack.Transfers[^1].Amount);
    }

    [Fact]
    public async Task ConfirmSatisfaction_ReleasesDigitalOrder()
    {
        var order = new Order
        {
            VendorPhone = "0801",
            CustomerName = "Chidi",
            CustomerPhone = "0802",
            DeliveryAddress = "Email delivery",
            AmountKobo = 2000000,
            Fulfillment = FulfillmentType.Digital,
            Status = OrderStatus.Held,
            PaystackReference = "ref-dig",
            VendorRecipientCode = "RCP_VENDOR"
        };
        await _orders.AddAsync(order, CancellationToken.None);
        await _orders.SaveAsync(CancellationToken.None);
        var handler = new ConfirmSatisfactionCommandHandler(
            _orders, _db, _paystack, Notifier(), _mapper);

        var result = await handler.Handle(new ConfirmSatisfactionCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Released, order.Status);
        Assert.Equal(2000000, _paystack.Transfers[^1].Amount);
    }

    // ---------------------------------------------------------------------
    // Guest-action state gates.
    //
    // The track page is anonymous, so the frontend cannot probe these states
    // and used to infer them from prose. Two of them (verify-otp and
    // confirm-satisfaction) are Held-ONLY, which is not what a reader of
    // "dispute is Held/Delivered" would assume. These pin the real contract
    // so the UI gates and docs/FRONTEND_API.md cannot drift from the handlers.
    // ---------------------------------------------------------------------

    private static Order DigitalHeldOrder() => new()
    {
        VendorPhone = "0801",
        CustomerName = "Chidi",
        CustomerPhone = "0802",
        DeliveryAddress = "Email delivery",
        AmountKobo = 2000000,
        Fulfillment = FulfillmentType.Digital,
        Status = OrderStatus.Held,
        OrderNumber = "IS-DIGITAL",
        PaystackReference = "ref-digital",
        VendorRecipientCode = "RCP_VENDOR",
        OtpHash = "HASH:testsalt12345678:123456.testsalt12345678",
        OtpExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
    };

    private VerifyOtpCommandHandler VerifyHandler() =>
        new(_orders, _db, _otp, _paystack, Notifier(), _mapper);

    private async Task<Order> SeedAsync(Order order)
    {
        await _orders.AddAsync(order, CancellationToken.None);
        await _orders.SaveAsync(CancellationToken.None);
        return order;
    }

    [Fact]
    public async Task VerifyOtp_DeliveredOrder_Conflicts()
    {
        // The exact case the track page hit: Delivered renders the verify
        // panel, then the endpoint rejects it. Held-only, never Delivered.
        var order = DigitalHeldOrder();
        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = DateTimeOffset.UtcNow;
        await SeedAsync(order);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => VerifyHandler().Handle(new VerifyOtpCommand(order.Id, "123456"), CancellationToken.None));

        Assert.Contains("Delivered", ex.Message);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Empty(_paystack.Transfers);
    }

    [Fact]
    public async Task VerifyOtp_HeldDigitalOrder_Releases()
    {
        // Proves Held IS accepted, so the track page's gate is Held-only.
        var order = await SeedAsync(DigitalHeldOrder());

        var result = await VerifyHandler().Handle(new VerifyOtpCommand(order.Id, "123456"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Released, order.Status);
        Assert.Equal(2000000, _paystack.Transfers[^1].Amount);
    }

    [Fact]
    public async Task VerifyOtp_HeldDispatchWithDriver_Rejected_PointAtDriverPortal()
    {
        var order = DispatchHeldOrder();
        order.DriverPhone = "0803";
        await SeedAsync(order);

        var result = await VerifyHandler().Handle(new VerifyOtpCommand(order.Id, "123456"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("driver portal", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_paystack.Transfers);
    }

    [Fact]
    public async Task VerifyOtp_HeldDispatchWithoutDriver_Releases()
    {
        var order = DispatchHeldOrder();
        order.DriverPhone = null;
        await SeedAsync(order);

        var result = await VerifyHandler().Handle(new VerifyOtpCommand(order.Id, "123456"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Released, order.Status);
    }

    [Fact]
    public async Task ConfirmSatisfaction_DeliveredOrder_Conflicts()
    {
        // Second easy mistake: Held-only, not Held/Delivered.
        var order = DigitalHeldOrder();
        order.Status = OrderStatus.Delivered;
        await SeedAsync(order);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => new ConfirmSatisfactionCommandHandler(_orders, _db, _paystack, Notifier(), _mapper)
                .Handle(new ConfirmSatisfactionCommand(order.Id), CancellationToken.None));

        Assert.Contains("Delivered", ex.Message);
        Assert.Empty(_paystack.Transfers);
    }

    [Fact]
    public async Task ConfirmSatisfaction_DispatchOrder_Rejected()
    {
        var order = DispatchHeldOrder();
        await SeedAsync(order);

        var result = await new ConfirmSatisfactionCommandHandler(_orders, _db, _paystack, Notifier(), _mapper)
            .Handle(new ConfirmSatisfactionCommand(order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("digital", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(OrderStatus.Held)]
    [InlineData(OrderStatus.Delivered)]
    public async Task Dispute_AcceptsHeldAndDelivered(OrderStatus status)
    {
        var order = DigitalHeldOrder();
        order.Status = status;
        await SeedAsync(order);

        var result = await new DisputeOrderCommandHandler(_orders, new PassSanitizer(), Notifier(), _mapper)
            .Handle(new DisputeOrderCommand(order.Id, "not as described"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Disputed, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Released)]
    [InlineData(OrderStatus.AwaitingPayment)]
    [InlineData(OrderStatus.Refunded)]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Dispute_RejectsEveryOtherStatus(OrderStatus status)
    {
        var order = DigitalHeldOrder();
        order.Status = status;
        await SeedAsync(order);

        await Assert.ThrowsAsync<ConflictException>(
            () => new DisputeOrderCommandHandler(_orders, new PassSanitizer(), Notifier(), _mapper)
                .Handle(new DisputeOrderCommand(order.Id, "too late"), CancellationToken.None));

        Assert.Equal(status, order.Status);
    }
}

