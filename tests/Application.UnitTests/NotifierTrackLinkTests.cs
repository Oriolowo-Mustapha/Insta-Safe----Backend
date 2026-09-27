using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Notifications;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class NotifierTrackLinkTests
{
    private sealed class FakeWa : IWhatsAppSender
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
        public List<(string To, string Subject, string Html)> Sent { get; } = new();
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
        {
            Sent.Add((toEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
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

    private static Order TestOrder() => new()
    {
        OrderNumber = "IS-8K4N2Q",
        VendorPhone = "0801",
        CustomerName = "Chidi",
        CustomerPhone = "0802",
        BuyerEmail = "buyer@example.com",
        DeliveryAddress = "Lekki",
        AmountKobo = 4500000,
        Status = OrderStatus.AwaitingPayment,
        PaystackAuthUrl = "https://pay.test/x",
        PayVirtualAccountNumber = "9876543210",
        PayVirtualAccountBank = "Wema",
        PayVirtualAccountName = "Ada"
    };

    private static OrderNotifier Notifier(FakeWa wa, FakeEmail email, Dictionary<string, string> cfg) => new(
        wa, email, new FakeVendors(), NullLogger<OrderNotifier>.Instance, new MapConfig(cfg));

    [Fact]
    public async Task PaymentLink_IncludesTrackUrl_WhenConfigured()
    {
        var wa = new FakeWa();
        var notifier = Notifier(wa, new FakeEmail(),
            new Dictionary<string, string> { ["Frontend:BaseUrl"] = "https://app.test/" });

        await notifier.PaymentLinkAsync(TestOrder());

        Assert.Contains("https://app.test/track/IS-8K4N2Q", wa.Sent[^1]);
        Assert.Contains("IS-8K4N2Q", wa.Sent[^1]);
    }

    [Fact]
    public async Task PaymentLink_OmitsTrackUrl_WhenUnconfigured()
    {
        var wa = new FakeWa();
        var notifier = Notifier(wa, new FakeEmail(), new Dictionary<string, string>());

        await notifier.PaymentLinkAsync(TestOrder());

        Assert.DoesNotContain("track", wa.Sent[^1]);
        Assert.Contains("IS-8K4N2Q", wa.Sent[^1]);
    }

    [Fact]
    public async Task BankTransferDetails_IncludesTrackLink()
    {
        var wa = new FakeWa();
        var notifier = Notifier(wa, new FakeEmail(),
            new Dictionary<string, string> { ["Frontend:BaseUrl"] = "https://app.test" });

        await notifier.BankTransferDetailsAsync(TestOrder());

        Assert.Contains("https://app.test/track/IS-8K4N2Q", wa.Sent[^1]);
    }
}
