using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Notifications;
using InstaSafe.Application.Features.Orders.Commands.MarkFundsHeld;
using InstaSafe.Application.Features.Orders.Commands.RequestBankTransfer;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Exceptions;
using InstaSafe.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class BankTransferTests : IDisposable
{
    private sealed class FakePaystack : IPaystackClient
    {
        public bool CustomerOk { get; set; } = true;
        public bool AssignOk { get; set; } = true;
        public bool VerifyOk { get; set; } = true;
        public int CustomerCalls { get; private set; }
        public Task<(string Reference, string AuthUrl)> InitializeTransactionAsync(
            string email, long amountKobo, Guid orderId, CancellationToken ct)
            => Task.FromResult(("ref", "https://pay.test"));
        public Task<bool> VerifyTransactionAsync(string reference, CancellationToken ct)
            => Task.FromResult(VerifyOk);
        public Task<string?> CreateRecipientAsync(string accountNumber, string bankCode, string name, CancellationToken ct)
            => Task.FromResult<string?>("RCP_TEST");
        public Task<string?> InitiateTransferAsync(long amountKobo, string recipientCode, string reason, CancellationToken ct)
            => Task.FromResult<string?>("TRF_TEST");
        public Task<(bool Success, string? RefundReference, string? Error)> RefundTransactionAsync(string reference, CancellationToken ct)
            => Task.FromResult((true, (string?)"RFND_T", (string?)null));
        public Task<(bool Success, string? CustomerCode, string? Error)> CreateCustomerAsync(
            string email, string firstName, string lastName, string phone, Guid orderId, CancellationToken ct)
        {
            CustomerCalls++;
            return Task.FromResult(CustomerOk
                ? (true, (string?)"CUS_T", (string?)null)
                : (false, (string?)null, "customer failed"));
        }
        public Task<(bool Success, string? AccountNumber, string? AccountName, string? Bank, string? Error)> AssignDedicatedAccountAsync(
            string customerCode, string? preferredBank, CancellationToken ct)
            => Task.FromResult(AssignOk
                ? (true, (string?)"9876543210", (string?)"Ada Obi", (string?)"Wema", (string?)null)
                : (false, (string?)null, (string?)null, (string?)null, "assign failed"));
        public Task<List<(string Name, string Slug, string Code)>> ListTransferBanksAsync(CancellationToken ct)
            => Task.FromResult(new List<(string Name, string Slug, string Code)>());
    }

    private sealed class FakeOtp : IOtpService
    {
        public string GenerateOtp(int digits = 6) => "123456";
        public string NewSalt() => "testsalt12345678";
        public string Hash(string otp, string salt) => $"HASH:{salt}:{otp}";
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
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct) => Task.FromResult(false);
        public Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Vendor?>(null);
        public Task<Vendor?> GetByPhoneAsync(string phone, CancellationToken ct) => Task.FromResult<Vendor?>(null);
        public Task<Vendor?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<Vendor?>(null);
        public Task<List<Vendor>> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct)
            => Task.FromResult(new List<Vendor>());
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification
            => Task.CompletedTask;
    }

    private sealed class PassSanitizer : ISanitizer
    {
        public string Clean(string? input, int maxLength = 2000)
            => string.IsNullOrEmpty(input) ? string.Empty : input.Length > maxLength ? input[..maxLength] : input;
    }

    private readonly AppDbContext _db;
    private readonly FakePaystack _paystack = new();
    private readonly IMapper _mapper;

    public BankTransferTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options, new FakePublisher());
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<OrderMappingProfile>(),
            NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();
    }

    public void Dispose() => _db.Dispose();

    private OrderNotifier Notifier() => new(new NullSender(), new NullEmail(), new NullVendors(),
        NullLogger<OrderNotifier>.Instance);

    private async Task<Order> AwaitingOrder(string email = "buyer@example.com", long kobo = 5000000)
    {
        var order = new Order
        {
            VendorPhone = "0801",
            CustomerName = "Chidi",
            CustomerPhone = "0802",
            BuyerEmail = email,
            DeliveryAddress = "Lekki",
            AmountKobo = kobo,
            Status = OrderStatus.AwaitingPayment
        };
        await _db.Orders.AddAsync(order);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return order;
    }

    // NOTE: production OrderRepository is used via interface subset below.
    private IOrderRepository Orders() => new ProdOrderRepo(_db);

    private sealed class ProdOrderRepo : IOrderRepository
    {
        private readonly AppDbContext _db;
        public ProdOrderRepo(AppDbContext db) => _db = db;
        public async Task AddAsync(Order order, CancellationToken ct) => await _db.Orders.AddAsync(order, ct);
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
            => _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct)!;
        public Task<Order?> GetByPaystackRefAsync(string reference, CancellationToken ct)
            => _db.Orders.FirstOrDefaultAsync(o => o.PaystackReference == reference, ct)!;
        public Task<List<Order>> ListAsync(int page, int pageSize, CancellationToken ct)
            => _db.Orders.ToListAsync(ct);
        public Task<List<Order>> ListByVendorAsync(Guid vendorId, string vendorPhone, int page, int pageSize, CancellationToken ct)
            => _db.Orders.Where(o => o.VendorId == vendorId || o.VendorPhone == vendorPhone).ToListAsync(ct);
        public Task<List<Order>> ListByDriverAsync(Guid driverId, string driverPhone, int page, int pageSize, CancellationToken ct)
            => _db.Orders.Where(o => o.DriverId == driverId || o.DriverPhone == driverPhone).ToListAsync(ct);
        public Task<List<Order>> ListUnpaidByEmailAsync(string email, CancellationToken ct)
            => _db.Orders.Where(o => (o.Status == OrderStatus.AwaitingPayment || o.Status == OrderStatus.Draft)
                && o.BuyerEmail != null && o.BuyerEmail.ToLower() == email.ToLower()).ToListAsync(ct);
        public Task SaveAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task RequestBankTransfer_IssuesAccount_AndStoresIt()
    {
        var order = await AwaitingOrder();
        var handler = new RequestBankTransferCommandHandler(Orders(), _paystack, Notifier(), _mapper);

        var result = await handler.Handle(new RequestBankTransferCommand(order.Id, "wema-bank"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("9876543210", result.Value!.PayVirtualAccountNumber);
        Assert.Equal("Wema", result.Value!.PayVirtualAccountBank);
        var reloaded = await _db.Orders.FindAsync(order.Id);
        Assert.Equal("CUS_T", reloaded!.PaystackCustomerCode);
        Assert.Equal("9876543210", reloaded.PayVirtualAccountNumber);
    }

    [Fact]
    public async Task RequestBankTransfer_ExistingAccount_IsIdempotent()
    {
        var order = await AwaitingOrder();
        order.PayVirtualAccountNumber = "0001112223";
        order.PaystackCustomerCode = "CUS_OLD";
        _db.Orders.Update(order);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var handler = new RequestBankTransferCommandHandler(Orders(), _paystack, Notifier(), _mapper);

        var result = await handler.Handle(new RequestBankTransferCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _paystack.CustomerCalls);
        Assert.Equal("0001112223", result.Value!.PayVirtualAccountNumber);
    }

    [Fact]
    public async Task RequestBankTransfer_HeldOrder_ThrowsConflict()
    {
        var order = await AwaitingOrder();
        order.Status = OrderStatus.Held;
        _db.Orders.Update(order);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var handler = new RequestBankTransferCommandHandler(Orders(), _paystack, Notifier(), _mapper);

        await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(new RequestBankTransferCommand(order.Id), CancellationToken.None));
    }

    [Fact]
    public async Task RequestBankTransfer_CustomerFailure_ReturnsFailure()
    {
        var order = await AwaitingOrder();
        _paystack.CustomerOk = false;
        var handler = new RequestBankTransferCommandHandler(Orders(), _paystack, Notifier(), _mapper);

        var result = await handler.Handle(new RequestBankTransferCommand(order.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(order.PayVirtualAccountNumber);
    }

    private MarkFundsHeldCommandHandler HeldHandler() => new(
        Orders(), _db, _paystack, new FakeOtp(), new NullSender(), _mapper);

    [Fact]
    public async Task MarkHeld_DvaPayment_MatchesByEmailAndAmount()
    {
        var order = await AwaitingOrder();
        var handler = HeldHandler();

        var result = await handler.Handle(
            new MarkFundsHeldCommand("DVA-txn-123", "buyer@example.com", 5000000), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Held, result.Value!.Status);
        Assert.Equal("DVA-txn-123", result.Value.PaystackReference);
        var reloaded = await _db.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Held, reloaded!.Status);
    }

    [Fact]
    public async Task MarkHeld_DvaPayment_WrongAmount_NotMatched()
    {
        var order = await AwaitingOrder();
        var handler = HeldHandler();

        var result = await handler.Handle(
            new MarkFundsHeldCommand("DVA-txn-123", "buyer@example.com", 4990000), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var reloaded = await _db.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.AwaitingPayment, reloaded!.Status);
    }

    [Fact]
    public async Task MarkHeld_DvaPayment_Ambiguous_NotMatched()
    {
        await AwaitingOrder();
        await AwaitingOrder();
        var handler = HeldHandler();

        var result = await handler.Handle(
            new MarkFundsHeldCommand("DVA-txn-123", "buyer@example.com", 5000000), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
