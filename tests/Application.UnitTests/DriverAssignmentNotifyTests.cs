using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Notifications;
using InstaSafe.Application.Features.Orders.Commands.CreateOrder;
using InstaSafe.Application.Features.Orders.Commands.MarkFundsHeld;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Exceptions;
using InstaSafe.Infrastructure.Persistence;
using InstaSafe.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

/// <summary>
/// Pins WHEN the rider is told about a delivery.
///
/// /api/dispatch/assigned only lists Held|Delivered. The assignment message
/// used to fire from CreateOrder, when the order was still AwaitingPayment —
/// so the rider got "delivery assigned" on WhatsApp and then opened the portal
/// to an empty list until the buyer actually paid. The message now fires from
/// MarkFundsHeld, the first state the rider can act on.
/// </summary>
public class DriverAssignmentNotifyTests : IDisposable
{
    private sealed class RecordingSender : IWhatsAppSender
    {
        public List<(string To, string Body)> Sent { get; } = new();
        public Task SendTextAsync(string toPhone, string body, CancellationToken ct)
        {
            Sent.Add((toPhone, body));
            return Task.CompletedTask;
        }
        public Task SendTemplateAsync(string toPhone, string templateName, Dictionary<string, string> vars, CancellationToken ct)
            => Task.CompletedTask;

        public List<string> Bodies => Sent.Select(s => s.Body).ToList();
        public int AssignmentCount => Sent.Count(s => s.Body.Contains("delivery assigned", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class NullEmail : IEmailSender
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

    private sealed class FakeDrivers : IDispatcherRepository
    {
        public Task AddAsync(Dispatcher dispatcher, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct) => Task.FromResult(false);
        public Task<Dispatcher?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Dispatcher?>(null);
        public Task<Dispatcher?> GetByPhoneAsync(string phone, CancellationToken ct) => Task.FromResult<Dispatcher?>(null);
        public Task<List<Dispatcher>> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct)
            => Task.FromResult(new List<Dispatcher>());
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePaystack : IPaystackClient
    {
        public string? LastCallbackUrl { get; private set; } = "unset";
        public Task<(string Reference, string AuthUrl)> InitializeTransactionAsync(
            string email, long amountKobo, Guid orderId, CancellationToken ct,
            string? callbackUrl = null)
        {
            LastCallbackUrl = callbackUrl;
            return Task.FromResult(("ref_test", "https://pay.test/x"));
        }
        public Task<bool> VerifyTransactionAsync(string reference, CancellationToken ct) => Task.FromResult(true);
        public Task<string?> CreateRecipientAsync(string accountNumber, string bankCode, string name, CancellationToken ct)
            => Task.FromResult<string?>("RCP_TEST");
        public Task<string?> InitiateTransferAsync(long amountKobo, string recipientCode, string reason, CancellationToken ct)
            => Task.FromResult<string?>("TRF_TEST");
        public Task<(bool Success, string? RefundReference, string? Error)> RefundTransactionAsync(string reference, CancellationToken ct)
            => Task.FromResult((true, (string?)"RFND_T", (string?)null));
        public Task<(bool Success, string? CustomerCode, string? Error)> CreateCustomerAsync(
            string email, string firstName, string lastName, string phone, Guid orderId, CancellationToken ct)
            => Task.FromResult((true, (string?)"CUS_T", (string?)null));
        public Task<(bool Success, string? AccountNumber, string? AccountName, string? Bank, string? Error)> AssignDedicatedAccountAsync(
            string customerCode, string? preferredBank, CancellationToken ct)
            => Task.FromResult((true, (string?)"9876543210", (string?)"Ada", (string?)"Wema", (string?)null));
        public Task<List<(string Name, string Slug, string Code)>> ListTransferBanksAsync(CancellationToken ct)
            => Task.FromResult(new List<(string Name, string Slug, string Code)>());
        public Task<AccountResolveResult> ResolveAccountAsync(string accountNumber, string bankCode, CancellationToken ct)
            => Task.FromResult(new AccountResolveResult(true, "Ada", ResolveFailureKind.Invalid, ""));
        public Task<List<(string Name, string Slug, string Code)>> ListAllBanksAsync(CancellationToken ct)
            => Task.FromResult(new List<(string Name, string Slug, string Code)>());
    }

    private sealed class FakeOtp : IOtpService
    {
        public string GenerateOtp(int digits = 6) => "123456";
        public string NewSalt() => "testsalt12345678";
        public string Hash(string otp, string salt) => $"HASH:{salt}:{otp}";
    }

    private sealed class PassSanitizer : ISanitizer
    {
        public string Clean(string? input, int maxLength = 2000)
            => string.IsNullOrEmpty(input) ? string.Empty : input.Length > maxLength ? input[..maxLength] : input;
    }

    private sealed class FakePublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification
            => Task.CompletedTask;
    }

    private const string DriverPhone = "2348055556666";

    private readonly AppDbContext _db;
    private readonly FakePaystack _paystack = new();
    private readonly IMapper _mapper;

    public DriverAssignmentNotifyTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options, new FakePublisher());
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<OrderMappingProfile>(), NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();
    }

    public void Dispose() => _db.Dispose();

    private OrderNotifier Notifier(IWhatsAppSender wa) =>
        new(wa, new NullEmail(), new FakeVendors(), NullLogger<OrderNotifier>.Instance);

    private IOrderRepository Orders() => new OrderRepository(_db);

    private MarkFundsHeldCommandHandler HeldHandler(RecordingSender wa) => new(
        Orders(), _db, _paystack, new FakeOtp(), wa, Notifier(wa), _mapper);

    private async Task<Order> SeedDispatchOrder(string driverPhone)
    {
        var order = new Order
        {
            VendorPhone = "2348010000000",
            CustomerName = "Chidi",
            CustomerPhone = "2348087654321",
            BuyerEmail = "buyer@example.com",
            DeliveryAddress = "Lekki Phase 1",
            AmountKobo = 5_000_000,
            DeliveryFeeKobo = 500_000,
            Fulfillment = FulfillmentType.Dispatch,
            Status = OrderStatus.AwaitingPayment,
            OrderNumber = "IS-8K4N2Q",
            DriverPhone = driverPhone,
            DriverId = Guid.NewGuid(),
            PaystackReference = "ref_test"
        };
        await _db.Orders.AddAsync(order);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return order;
    }

    [Fact]
    public async Task MarkHeld_DispatchOrder_NotifiesDriver()
    {
        var wa = new RecordingSender();
        await SeedDispatchOrder(DriverPhone);

        var result = await HeldHandler(wa).Handle(
            new MarkFundsHeldCommand("ref_test", null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Held, result.Value!.Status);
        Assert.Equal(1, wa.AssignmentCount);
        Assert.Contains(wa.Sent, s => s.To == DriverPhone);
    }

    [Fact]
    public async Task MarkHeld_NotifiesDriver_OnlyAfterFundsAreHeld()
    {
        var wa = new RecordingSender();
        await SeedDispatchOrder(DriverPhone);

        await HeldHandler(wa).Handle(new MarkFundsHeldCommand("ref_test", null, null), CancellationToken.None);

        // The rider is only ever told after the order is visible in
        // /api/dispatch/assigned, which filters on Held|Delivered.
        var reloaded = await _db.Orders.FirstAsync();
        Assert.Equal(OrderStatus.Held, reloaded.Status);
        Assert.Equal(1, wa.AssignmentCount);
    }

    [Fact]
    public async Task MarkHeld_OrderWithoutDriver_DoesNotNotifyDriver()
    {
        var wa = new RecordingSender();
        await SeedDispatchOrder(driverPhone: null!);

        var result = await HeldHandler(wa).Handle(
            new MarkFundsHeldCommand("ref_test", null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, wa.AssignmentCount);
    }

    [Fact]
    public async Task MarkHeld_ReplayedWebhook_ConflictsAndDoesNotNotifyTwice()
    {
        var wa = new RecordingSender();
        await SeedDispatchOrder(DriverPhone);
        var handler = HeldHandler(wa);

        await handler.Handle(new MarkFundsHeldCommand("ref_test", null, null), CancellationToken.None);
        Assert.Equal(1, wa.AssignmentCount);

        // A duplicate Paystack webhook must not re-text the rider.
        await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(new MarkFundsHeldCommand("ref_test", null, null), CancellationToken.None));
        Assert.Equal(1, wa.AssignmentCount);
    }

    [Fact]
    public async Task CreateOrder_DoesNotNotifyDriver_BeforeFundsAreHeld()
    {
        var wa = new RecordingSender();
        var handler = new CreateOrderCommandHandler(
            Orders(), new FakeVendors(), new FakeDrivers(), _paystack,
            new PassSanitizer(), Notifier(wa), _mapper);

        var result = await handler.Handle(new CreateOrderCommand(
            VendorPhone: "08010000000",
            CustomerName: "Chidi",
            CustomerPhone: "08087654321",
            DeliveryAddress: "Lekki Phase 1",
            Items: [new OrderItemInput("Sneakers", 2, 22_500)],
            AmountNgn: 45_000,
            BuyerEmail: "buyer@example.com",
            Fulfillment: FulfillmentType.Dispatch,
            DeliveryFeeNgn: 5_000,
            DriverPhone: DriverPhone), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.AwaitingPayment, result.Value!.Status);
        // The regression this suite exists for.
        Assert.Equal(0, wa.AssignmentCount);
    }

    [Fact]
    public async Task CreateOrder_PassesTrackCallback_WhenFrontendConfigured()
    {
        var wa = new RecordingSender();
        var handler = new CreateOrderCommandHandler(
            Orders(), new FakeVendors(), new FakeDrivers(), _paystack,
            new PassSanitizer(), Notifier(wa), _mapper,
            new MapConfig(new Dictionary<string, string>
                { ["Frontend:BaseUrl"] = "https://app.test/" }));

        var result = await handler.Handle(DispatchCmd(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            $"https://app.test/track/{result.Value!.OrderNumber}",
            _paystack.LastCallbackUrl);
    }

    [Fact]
    public async Task CreateOrder_OmitsCallback_WhenFrontendUnconfigured()
    {
        var wa = new RecordingSender();
        var handler = new CreateOrderCommandHandler(
            Orders(), new FakeVendors(), new FakeDrivers(), _paystack,
            new PassSanitizer(), Notifier(wa), _mapper);

        var result = await handler.Handle(DispatchCmd(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(_paystack.LastCallbackUrl);
    }

    private static CreateOrderCommand DispatchCmd() => new(
        VendorPhone: "08010000000",
        CustomerName: "Chidi",
        CustomerPhone: "08087654321",
        DeliveryAddress: "Lekki Phase 1",
        Items: [new OrderItemInput("Sneakers", 2, 22_500)],
        AmountNgn: 45_000,
        BuyerEmail: "buyer@example.com",
        Fulfillment: FulfillmentType.Dispatch,
        DeliveryFeeNgn: 5_000,
        DriverPhone: DriverPhone);

    private sealed class MapConfig : Microsoft.Extensions.Configuration.IConfiguration
    {
        private readonly Dictionary<string, string> _values;
        public MapConfig(Dictionary<string, string> values) => _values = values;
        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var v) ? v : null;
            set { }
        }
        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }
}
