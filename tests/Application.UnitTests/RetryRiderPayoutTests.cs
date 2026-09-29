using AutoMapper;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Notifications;
using InstaSafe.Application.Features.Admin.Commands.RetryRiderPayout;
using InstaSafe.Application.Features.Dispatch.Commands.ConfirmDelivery;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Exceptions;
using InstaSafe.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

/// <summary>
/// The rider-fee incident: a delivery confirmed but the fee transfer failed,
/// and nothing recorded it - no log context, no notification, no admin
/// visibility. These pin the fix: the failure is loud (Error log + outbox
/// RiderPayoutFailed, delivery still completes) and recoverable (admin retry
/// that can never pay twice).
/// </summary>
public class RetryRiderPayoutTests : IDisposable
{
    private sealed class FakePaystack : IPaystackClient
    {
        public List<(long Amount, string Recipient)> Transfers { get; } = new();
        public string? TransferRef { get; set; } = "TRF_RIDER_NEW";
        public Task<(string Reference, string AuthUrl)> InitializeTransactionAsync(
            string email, long amountKobo, Guid orderId, CancellationToken ct)
            => Task.FromResult(("ref", "https://pay.test"));
        public Task<bool> VerifyTransactionAsync(string reference, CancellationToken ct) => Task.FromResult(true);
        public Task<string?> CreateRecipientAsync(string accountNumber, string bankCode, string name, CancellationToken ct)
            => Task.FromResult<string?>("RCP_TEST");
        public Task<string?> InitiateTransferAsync(long amountKobo, string recipientCode, string reason, CancellationToken ct)
        {
            Transfers.Add((amountKobo, recipientCode));
            return Task.FromResult(TransferRef);
        }
        public Task<(bool Success, string? RefundReference, string? Error)> RefundTransactionAsync(string reference, CancellationToken ct)
            => Task.FromResult((true, (string?)"RFND_T", (string?)null));
        public Task<(bool Success, string? CustomerCode, string? Error)> CreateCustomerAsync(
            string email, string firstName, string lastName, string phone, Guid orderId, CancellationToken ct)
            => Task.FromResult((true, (string?)"CUS_T", (string?)null));
        public Task<(bool Success, string? AccountNumber, string? AccountName, string? Bank, string? Error)> AssignDedicatedAccountAsync(
            string customerCode, string? preferredBank, CancellationToken ct)
            => Task.FromResult((true, (string?)"0123456789", (string?)"Ada", (string?)"Wema", (string?)null));
        public Task<List<(string Name, string Slug, string Code)>> ListTransferBanksAsync(CancellationToken ct)
            => Task.FromResult(new List<(string Name, string Slug, string Code)>());
        public Task<List<(string Name, string Slug, string Code)>> ListAllBanksAsync(CancellationToken ct)
            => Task.FromResult(new List<(string Name, string Slug, string Code)>());
        public Task<AccountResolveResult> ResolveAccountAsync(string accountNumber, string bankCode, CancellationToken ct)
            => Task.FromResult(new AccountResolveResult(true, "Ada", ResolveFailureKind.Invalid, ""));
    }

    private sealed class FakeOtp : IOtpService
    {
        public string GenerateOtp(int digits = 6) => "123456";
        public string NewSalt() => "testsalt12345678";
        public string Hash(string otp, string salt) => $"HASH:{salt}:{otp}";
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
        public bool IsConfigured => false;
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeVendors : IVendorRepository
    {
        public Task AddAsync(Vendor vendor, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct) => Task.FromResult(false);
        public Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Vendor?>(null);
        public Task<Vendor?> GetByPhoneAsync(string phone, CancellationToken ct) => Task.FromResult<Vendor?>(null);
        public Task<Vendor?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<Vendor?>(null);
        public Task<List<Vendor>> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct)
            => Task.FromResult(new List<Vendor>());
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

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

    private sealed class FakePublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification
            => Task.CompletedTask;
    }

    private readonly AppDbContext _db;
    private readonly FakePaystack _paystack = new();
    private readonly FakeSender _wa = new();
    private readonly IMapper _mapper;

    public RetryRiderPayoutTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options, new FakePublisher());
        _mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<OrderMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
    }

    public void Dispose() => _db.Dispose();

    private OrderNotifier Notifier() => new(_wa, new FakeEmail(), new FakeVendors(),
        NullLogger<OrderNotifier>.Instance);

    private static Order DeliveredUnpaidRiderOrder() => new()
    {
        VendorPhone = "0801",
        CustomerName = "Chidi",
        CustomerPhone = "0802",
        DeliveryAddress = "Lekki",
        AmountKobo = 5000000,
        DeliveryFeeKobo = 500000,
        Fulfillment = FulfillmentType.Dispatch,
        Status = OrderStatus.Delivered,
        OrderNumber = "IS-RIDER1",
        PaystackReference = "ref-rider",
        VendorRecipientCode = "RCP_VENDOR",
        DriverPhone = "0803",
        DriverRecipientCode = "RCP_DRIVER",
        OtpHash = "HASH:testsalt12345678:123456.testsalt12345678",
        OtpExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
    };

    private RetryRiderPayoutCommandHandler RetryHandler() => new(
        new FakeOrders(_db), _paystack, _mapper,
        NullLogger<RetryRiderPayoutCommandHandler>.Instance);

    private async Task<Order> SeedAsync(Order order)
    {
        await _db.Orders.AddAsync(order);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return order;
    }

    [Fact]
    public async Task Retry_SendsRiderFee_WritesReference()
    {
        var order = await SeedAsync(DeliveredUnpaidRiderOrder());

        var result = await RetryHandler().Handle(
            new RetryRiderPayoutCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("TRF_RIDER_NEW", result.Value!.DriverTransferReference);
        Assert.Equal("TRF_RIDER_NEW", (await _db.Orders.FindAsync(order.Id))!.DriverTransferReference);
        Assert.Single(_paystack.Transfers);
        Assert.Equal(500000, _paystack.Transfers[0].Amount);
        Assert.Equal("RCP_DRIVER", _paystack.Transfers[0].Recipient);
    }

    [Fact]
    public async Task Retry_RefusesWhenAlreadyPaid()
    {
        var seed = DeliveredUnpaidRiderOrder();
        seed.DriverTransferReference = "TRF_OLD";
        var order = await SeedAsync(seed);

        await Assert.ThrowsAsync<ConflictException>(
            () => RetryHandler().Handle(new RetryRiderPayoutCommand(order.Id), CancellationToken.None));
        Assert.Empty(_paystack.Transfers);
    }

    [Fact]
    public async Task Retry_RefusesWhenNoRecipient()
    {
        var seed = DeliveredUnpaidRiderOrder();
        seed.DriverRecipientCode = null;
        var order = await SeedAsync(seed);

        await Assert.ThrowsAsync<ConflictException>(
            () => RetryHandler().Handle(new RetryRiderPayoutCommand(order.Id), CancellationToken.None));
        Assert.Empty(_paystack.Transfers);
    }

    [Fact]
    public async Task Retry_RefusesWhenNotDelivered()
    {
        var seed = DeliveredUnpaidRiderOrder();
        seed.Status = OrderStatus.Held;
        var order = await SeedAsync(seed);

        await Assert.ThrowsAsync<ConflictException>(
            () => RetryHandler().Handle(new RetryRiderPayoutCommand(order.Id), CancellationToken.None));
        Assert.Empty(_paystack.Transfers);
    }

    [Fact]
    public async Task Retry_PaystackRejects_ReturnsFailure_WritesNothing()
    {
        var order = await SeedAsync(DeliveredUnpaidRiderOrder());
        _paystack.TransferRef = null;

        var result = await RetryHandler().Handle(
            new RetryRiderPayoutCommand(order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null((await _db.Orders.FindAsync(order.Id))!.DriverTransferReference);
    }

    [Fact]
    public async Task Confirm_TransferFails_DeliversAnyway_RecordsOutboxError()
    {
        // The incident: Paystack rejects the fee, but the handover happened.
        // The order must still complete AND the failure must be visible.
        var seed = DeliveredUnpaidRiderOrder();
        seed.Status = OrderStatus.Held;
        var order = await SeedAsync(seed);
        _paystack.TransferRef = null;
        var handler = new ConfirmDeliveryCommandHandler(
            new FakeOrders(_db), _db, new FakeOtp(), _paystack, Notifier(), _mapper,
            NullLogger<ConfirmDeliveryCommandHandler>.Instance);

        var result = await handler.Handle(
            new ConfirmDeliveryCommand(Guid.NewGuid(), "0803", order.Id, "123456"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var reloaded = await _db.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Delivered, reloaded!.Status);
        Assert.Null(reloaded.DriverTransferReference);

        var failure = await _db.OutboxMessages
            .FirstOrDefaultAsync(m => m.Type == "RiderPayoutFailed");
        Assert.NotNull(failure);
        Assert.NotNull(failure.Error);
        Assert.Contains("IS-RIDER1", failure.Payload);
    }

    [Fact]
    public async Task Confirm_TransferSucceeds_WritesNoFailure()
    {
        var seed = DeliveredUnpaidRiderOrder();
        seed.Status = OrderStatus.Held;
        var order = await SeedAsync(seed);
        var handler = new ConfirmDeliveryCommandHandler(
            new FakeOrders(_db), _db, new FakeOtp(), _paystack, Notifier(), _mapper,
            NullLogger<ConfirmDeliveryCommandHandler>.Instance);

        var result = await handler.Handle(
            new ConfirmDeliveryCommand(Guid.NewGuid(), "0803", order.Id, "123456"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("TRF_RIDER_NEW", (await _db.Orders.FindAsync(order.Id))!.DriverTransferReference);
        Assert.Empty(await _db.OutboxMessages
            .Where(m => m.Type == "RiderPayoutFailed").ToListAsync());
    }
}
