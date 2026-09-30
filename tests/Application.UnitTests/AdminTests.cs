using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Features.Admin.Commands.ForceRelease;
using InstaSafe.Application.Features.Admin.Queries.GetAdminStats;
using InstaSafe.Application.Features.Auth.Commands.VendorLogin;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Exceptions;
using InstaSafe.Infrastructure.Persistence;
using InstaSafe.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class AdminTests : IDisposable
{
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

    private sealed class FakeTokens : IJwtTokenService
    {
        public string CreateVendorToken(Guid vendorId, string phone) => $"V:{vendorId}";
        public string CreateDispatcherToken(Guid dispatcherId, string phone) => $"D:{dispatcherId}";
        public string CreateAdminToken(string email) => $"ADMIN:{email}";
    }

    private sealed class FakePaystack : IPaystackClient
    {
        public List<(long Amount, string Recipient)> Transfers { get; } = new();
        public Task<(string Reference, string AuthUrl)> InitializeTransactionAsync(
            string email, long amountKobo, Guid orderId, CancellationToken ct,
            string? callbackUrl = null)
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

    private sealed class FakeSender : IWhatsAppSender
    {
        public Task SendTextAsync(string toPhone, string body, CancellationToken ct) => Task.CompletedTask;
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

    private sealed class PassSanitizer : ISanitizer
    {
        public string Clean(string? input, int maxLength = 2000)
            => string.IsNullOrEmpty(input) ? string.Empty : input.Length > maxLength ? input[..maxLength] : input;
    }

    private readonly AppDbContext _db;
    private readonly FakePaystack _paystack = new();
    private readonly IMapper _mapper;

    public AdminTests()
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

    private InstaSafe.Application.Common.Notifications.OrderNotifier Notifier() => new(
        new FakeSender(), new FakeEmail(), new FakeVendors(),
        NullLogger<InstaSafe.Application.Common.Notifications.OrderNotifier>.Instance);

    [Fact]
    public async Task AdminLogin_CorrectPassword_ReturnsAdminRole()
    {
        var hasher = new PasswordHasher();
        var config = new MapConfig(new Dictionary<string, string>
        {
            ["Admin:Email"] = "admin@instasafe.ng",
            ["Admin:PasswordHash"] = hasher.Hash("SuperSecret123")
        });
        var handler = new VendorLoginCommandHandler(
            new FakeVendors(), hasher, new FakeTokens(), _mapper, config);

        var result = await handler.Handle(
            new VendorLoginCommand("ADMIN@instasafe.ng", "SuperSecret123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("admin", result.Value!.Role);
        Assert.Null(result.Value.Vendor);
        Assert.StartsWith("ADMIN:", result.Value.Token);
    }

    [Fact]
    public async Task AdminLogin_WrongPassword_Fails()
    {
        var hasher = new PasswordHasher();
        var config = new MapConfig(new Dictionary<string, string>
        {
            ["Admin:Email"] = "admin@instasafe.ng",
            ["Admin:PasswordHash"] = hasher.Hash("SuperSecret123")
        });
        var handler = new VendorLoginCommandHandler(
            new FakeVendors(), hasher, new FakeTokens(), _mapper, config);

        var result = await handler.Handle(
            new VendorLoginCommand("admin@instasafe.ng", "wrong"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ForceRelease_HeldOrder_PaysRemainder()
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
            Status = OrderStatus.Held,
            VendorRecipientCode = "RCP_VENDOR"
        };
        await _db.Orders.AddAsync(order);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var handler = new ForceReleaseOrderCommandHandler(
            new FakeOrders(_db), _db, _paystack, Notifier(), _mapper);

        var result = await handler.Handle(
            new ForceReleaseOrderCommand(order.Id, "ops note"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Released, result.Value!.Status);
        Assert.Equal(4500000, _paystack.Transfers[^1].Amount);
    }

    [Fact]
    public async Task ForceRelease_ReleasedOrder_ThrowsConflict()
    {
        var order = new Order
        {
            VendorPhone = "0801",
            CustomerName = "Chidi",
            CustomerPhone = "0802",
            DeliveryAddress = "Lekki",
            AmountKobo = 1000,
            Status = OrderStatus.Released
        };
        await _db.Orders.AddAsync(order);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var handler = new ForceReleaseOrderCommandHandler(
            new FakeOrders(_db), _db, _paystack, Notifier(), _mapper);

        await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(new ForceReleaseOrderCommand(order.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Stats_AggregatesBuckets()
    {
        _db.Vendors.Add(new Vendor { Phone = "0801", DisplayName = "A", IsActive = true });
        _db.Vendors.Add(new Vendor { Phone = "0802", DisplayName = "B", IsActive = false });
        _db.Orders.Add(new Order
        {
            VendorPhone = "0801", CustomerName = "C", CustomerPhone = "0803",
            DeliveryAddress = "X", AmountKobo = 100000, Status = OrderStatus.Held
        });
        _db.Orders.Add(new Order
        {
            VendorPhone = "0801", CustomerName = "D", CustomerPhone = "0804",
            DeliveryAddress = "Y", AmountKobo = 200000, Status = OrderStatus.Disputed
        });
        await _db.SaveChangesAsync();
        var handler = new GetAdminStatsQueryHandler(_db);

        var result = await handler.Handle(new GetAdminStatsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.VendorTotal);
        Assert.Equal(1, result.Value.VendorActive);
        Assert.Equal(100000, result.Value.HeldGmvKobo);
        Assert.Equal(1, result.Value.OpenDisputes);
        Assert.Equal(1, result.Value.OrdersByStatus["Held"]);
    }
}

