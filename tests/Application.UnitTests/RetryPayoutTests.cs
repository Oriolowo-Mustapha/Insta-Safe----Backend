using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Features.Admin.Commands.RetryPayout;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Exceptions;
using InstaSafe.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class RetryPayoutTests : IDisposable
{
    private sealed class FakePaystack : IPaystackClient
    {
        public List<(long Amount, string Recipient)> Transfers { get; } = new();
        public string? TransferRef { get; set; } = "TRF_NEW";
        public Exception? ThrowOnTransfer { get; set; }

        public Task<(string Reference, string AuthUrl)> InitializeTransactionAsync(
            string email, long amountKobo, Guid orderId, CancellationToken ct)
            => Task.FromResult(("ref", "https://pay.test"));
        public Task<bool> VerifyTransactionAsync(string reference, CancellationToken ct) => Task.FromResult(true);
        public Task<string?> CreateRecipientAsync(string accountNumber, string bankCode, string name, CancellationToken ct)
            => Task.FromResult<string?>("RCP_TEST");
        public Task<string?> InitiateTransferAsync(long amountKobo, string recipientCode, string reason, CancellationToken ct)
        {
            if (ThrowOnTransfer is not null) throw ThrowOnTransfer;
            Transfers.Add((amountKobo, recipientCode));
            return Task.FromResult(TransferRef);
        }
        public Task<(bool Success, string? RefundReference, string? Error)> RefundTransactionAsync(string reference, CancellationToken ct)
            => Task.FromResult((true, (string?)"RFND_T", (string?)null));
        public Task<(bool Success, string? CustomerCode, string? Error)> CreateCustomerAsync(string email, string firstName, string lastName, string phone, Guid orderId, CancellationToken ct)
            => Task.FromResult((true, (string?)"CUS_T", (string?)null));
        public Task<(bool Success, string? AccountNumber, string? AccountName, string? Bank, string? Error)> AssignDedicatedAccountAsync(string customerCode, string? preferredBank, CancellationToken ct)
            => Task.FromResult((true, (string?)"0123456789", (string?)"Ada", (string?)"Wema", (string?)null));
        public Task<List<(string Name, string Slug, string Code)>> ListTransferBanksAsync(CancellationToken ct)
            => Task.FromResult(new List<(string Name, string Slug, string Code)>());
        public Task<List<(string Name, string Slug, string Code)>> ListAllBanksAsync(CancellationToken ct)
            => Task.FromResult(new List<(string Name, string Slug, string Code)>());
        public Task<AccountResolveResult> ResolveAccountAsync(string accountNumber, string bankCode, CancellationToken ct)
            => Task.FromResult(new AccountResolveResult(true, "Ada", ResolveFailureKind.Invalid, ""));
    }

    private sealed class FakeOrders : IOrderRepository
    {
        private readonly AppDbContext _db;
        public FakeOrders(AppDbContext db) => _db = db;
        public async Task AddAsync(Order order, CancellationToken ct) => await _db.Orders.AddAsync(order, ct);
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
            => _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct)!;
        public Task<Order?> GetByPaystackRefAsync(string reference, CancellationToken ct)
            => _db.Orders.FirstOrDefaultAsync(o => o.PaystackReference == reference, ct)!;
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
    private readonly IMapper _mapper;

    public RetryPayoutTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options, new FakePublisher());
        _mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<OrderMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
    }

    public void Dispose() => _db.Dispose();

    private RetryPayoutCommandHandler Handler() => new(
        new FakeOrders(_db), _db, _paystack, _mapper,
        NullLogger<RetryPayoutCommandHandler>.Instance);

    private async Task<Order> SeedReleasedOrderAsync(
        string? transferReference = null, string? recipientCode = "RCP_VENDOR",
        bool withLedger = true)
    {
        var order = new Order
        {
            VendorPhone = "0801",
            CustomerName = "Chidi",
            CustomerPhone = "0802",
            DeliveryAddress = "Lekki",
            AmountKobo = 5000000,
            DeliveryFeeKobo = 500000,
            DriverTransferReference = "TRF_DRIVER",
            Status = OrderStatus.Released,
            ReleasedAt = DateTimeOffset.UtcNow.AddHours(-2),
            TransferReference = transferReference,
            VendorRecipientCode = recipientCode
        };
        await _db.Orders.AddAsync(order);
        if (withLedger)
            _db.Ledgers.Add(new EscrowLedger
            {
                OrderId = order.Id,
                AmountKobo = 4500000,
                HeldAt = DateTimeOffset.UtcNow.AddDays(-1),
                ReleasedAt = DateTimeOffset.UtcNow.AddHours(-2)
            });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return order;
    }

    [Fact]
    public async Task RetryPayout_ReleasedWithoutTransfer_PaysRemainderAndRecordsReference()
    {
        var order = await SeedReleasedOrderAsync();

        var result = await Handler().Handle(new RetryPayoutCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("TRF_NEW", result.Value!.TransferReference);
        var transfer = Assert.Single(_paystack.Transfers);
        Assert.Equal(4500000, transfer.Amount);
        Assert.Equal("RCP_VENDOR", transfer.Recipient);
        _db.ChangeTracker.Clear();
        var stored = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal("TRF_NEW", stored.TransferReference);
        var ledger = await _db.Ledgers.AsNoTracking().SingleAsync(l => l.OrderId == order.Id);
        Assert.Equal("TRF_NEW", ledger.TransferReference);
    }

    [Fact]
    public async Task RetryPayout_PaystackRejects_LeavesOrderWithoutReference()
    {
        _paystack.TransferRef = null;
        var order = await SeedReleasedOrderAsync();

        var result = await Handler().Handle(new RetryPayoutCommand(order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("rejected", result.Error);
        _db.ChangeTracker.Clear();
        var stored = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Null(stored.TransferReference);
    }

    [Fact]
    public async Task RetryPayout_PaystackThrows_DoesNotPersistAndFlagsUnknownOutcome()
    {
        _paystack.ThrowOnTransfer = new HttpRequestException("socket closed");
        var order = await SeedReleasedOrderAsync();

        var result = await Handler().Handle(new RetryPayoutCommand(order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("unknown", result.Error);
        _db.ChangeTracker.Clear();
        var stored = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Null(stored.TransferReference);
    }

    [Fact]
    public async Task RetryPayout_AlreadyHasTransferReference_RefusesToPayAgain()
    {
        var order = await SeedReleasedOrderAsync(transferReference: "TRF_FIRST");

        await Assert.ThrowsAsync<ConflictException>(
            () => Handler().Handle(new RetryPayoutCommand(order.Id), CancellationToken.None));

        Assert.Empty(_paystack.Transfers);
    }

    [Fact]
    public async Task RetryPayout_NonReleasedOrder_ThrowsConflict()
    {
        var order = await SeedReleasedOrderAsync();
        var tracked = await _db.Orders.SingleAsync(o => o.Id == order.Id);
        tracked.Status = OrderStatus.Held;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<ConflictException>(
            () => Handler().Handle(new RetryPayoutCommand(order.Id), CancellationToken.None));

        Assert.Empty(_paystack.Transfers);
    }

    [Fact]
    public async Task RetryPayout_MissingRecipient_ThrowsConflict()
    {
        var order = await SeedReleasedOrderAsync(recipientCode: null);

        await Assert.ThrowsAsync<ConflictException>(
            () => Handler().Handle(new RetryPayoutCommand(order.Id), CancellationToken.None));

        Assert.Empty(_paystack.Transfers);
    }

    [Fact]
    public async Task RetryPayout_NothingLeftToPay_Fails()
    {
        var order = await SeedReleasedOrderAsync();
        var tracked = await _db.Orders.SingleAsync(o => o.Id == order.Id);
        tracked.AmountKobo = 500000;
        tracked.DeliveryFeeKobo = 500000;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var result = await Handler().Handle(new RetryPayoutCommand(order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(_paystack.Transfers);
    }

    [Fact]
    public async Task RetryPayout_DoesNotRequeueFundsReleasedNotification()
    {
        var order = await SeedReleasedOrderAsync();

        var result = await Handler().Handle(new RetryPayoutCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(await _db.OutboxMessages.ToListAsync());
    }
}
