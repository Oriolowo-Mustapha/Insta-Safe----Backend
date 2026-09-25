using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.Commands.CreateOrder;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Application.Features.Webhooks;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class ConversationRouterTests
{
    private sealed class FakeStates : IConversationRepository
    {
        public Dictionary<string, ConversationState> Store { get; } = new();
        public Task<ConversationState?> GetByPhoneAsync(string phone, CancellationToken ct)
            => Task.FromResult(Store.TryGetValue(phone, out var s) ? s : null);
        public Task AddAsync(ConversationState state, CancellationToken ct)
        {
            Store[state.Phone] = state;
            return Task.CompletedTask;
        }
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeVendors : IVendorRepository
    {
        public List<Vendor> Vendors { get; } = new();
        public Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Vendors.FirstOrDefault(v => v.Id == id));
        public Task<Vendor?> GetByPhoneAsync(string phone, CancellationToken ct)
            => Task.FromResult(Vendors.FirstOrDefault(v => v.Phone == phone));
        public Task<Vendor?> GetByEmailAsync(string email, CancellationToken ct)
            => Task.FromResult(Vendors.FirstOrDefault(v =>
                v.Email != null && v.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct)
            => Task.FromResult(Vendors.Any(v =>
                v.Email != null && v.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
        public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct)
            => Task.FromResult(Vendors.Any(v => v.Phone == phone));
        public Task AddAsync(Vendor vendor, CancellationToken ct)
        {
            Vendors.Add(vendor);
            return Task.CompletedTask;
        }
        public Task<List<Vendor>> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct)
            => Task.FromResult(Vendors.ToList());
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

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
    }

    private sealed class FakeParser : IGroqParser
    {
        public ChatIntent NextIntent { get; set; } = new(ChatIntentKind.Unknown, null, null);
        public ParsedOrder NextParsed { get; set; } = new("", "", "", new List<ParsedItem>(), 0);
        public Task<ChatIntent> ClassifyIntentAsync(string rawText, CancellationToken ct)
            => Task.FromResult(NextIntent);
        public Task<ParsedOrder> ParseOrderTextAsync(string rawText, CancellationToken ct)
            => Task.FromResult(NextParsed);
    }

    private sealed class FakeSender : IWhatsAppSender
    {
        public List<(string To, string Body)> Sent { get; } = new();
        public string LastBody => Sent.Count == 0 ? "" : Sent[^1].Body;
        public Task SendTextAsync(string toPhone, string body, CancellationToken ct)
        {
            Sent.Add((toPhone, body));
            return Task.CompletedTask;
        }
        public Task SendTemplateAsync(string toPhone, string templateName, Dictionary<string, string> vars, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class FakeMediator : IMediator
    {
        public Func<CreateOrderCommand, Result<OrderDto>>? OnCreateOrder;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is CreateOrderCommand cmd && OnCreateOrder is not null)
                return Task.FromResult((TResponse)(object)OnCreateOrder(cmd));
            throw new NotImplementedException();
        }
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotImplementedException();
        public Task<object?> Send(object request, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification
            => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IRequest<IAsyncEnumerable<TResponse>> request, CancellationToken ct = default)
            => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class PassThroughSanitizer : ISanitizer
    {
        public string Clean(string? input, int maxLength = 2000)
            => string.IsNullOrEmpty(input) ? string.Empty : input.Length > maxLength ? input[..maxLength] : input;
    }

    private sealed class FakeConfig : Microsoft.Extensions.Configuration.IConfiguration
    {
        public string? this[string key] { get => "https://app.test"; set { } }
        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    private readonly FakeStates _states = new();
    private readonly FakeVendors _vendors = new();
    private readonly FakeOrders _orders = new();
    private readonly FakeParser _parser = new();
    private readonly FakeSender _sender = new();
    private readonly FakeMediator _mediator = new();
    private readonly ConversationRouter _router;

    public ConversationRouterTests()
    {
        _router = new ConversationRouter(
            _states, _vendors, _orders, _parser, _sender, _mediator,
            new PassThroughSanitizer(), new FakeConfig(), NullLogger<ConversationRouter>.Instance);
    }

    private void SeedVendor(string phone) => _vendors.Vendors.Add(new Vendor
    {
        Phone = InstaSafe.Application.Common.Helpers.PhoneNormalizer.Normalize(phone),
        DisplayName = "Test Vendor",
        EmailVerified = true,
        OnboardingCompleted = true
    });

    private Task<bool> Send(string phone, string text)
    {
        if (!_vendors.Vendors.Any(v => v.Phone ==
            InstaSafe.Application.Common.Helpers.PhoneNormalizer.Normalize(phone)))
            SeedVendor(phone);
        return _router.RouteAsync(phone, text, null, CancellationToken.None);
    }

    private ConversationState State(string phone) => _states.Store[phone];

    [Fact]
    public async Task Greeting_ShowsWelcomeAndMenu()
    {
        _parser.NextIntent = new ChatIntent(ChatIntentKind.Greeting, null, null);

        await Send("08010000001", "Hi");

        Assert.Contains("Welcome to InstaSafe", _sender.LastBody);
        Assert.Contains("1️⃣", _sender.LastBody);
        Assert.Equal(ConversationStep.AwaitingMenuChoice, State("08010000001").Step);
    }

    [Fact]
    public async Task MenuDigitOne_StartsOrderDraft()
    {
        await Send("08010000002", "1");

        Assert.Contains("customer", _sender.LastBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConversationStep.DraftCustomerName, State("08010000002").Step);
    }

    [Fact]
    public async Task UnknownMessage_ShowsMenuNudge()
    {
        _parser.NextIntent = new ChatIntent(ChatIntentKind.Unknown, null, null);

        await Send("08010000003", "blah blah");

        Assert.Contains("didn't quite get that", _sender.LastBody);
        Assert.Equal(ConversationStep.AwaitingMenuChoice, State("08010000003").Step);
    }

    [Fact]
    public async Task Cancel_ResetsToIdle()
    {
        await Send("08010000004", "1");
        Assert.Equal(ConversationStep.DraftCustomerName, State("08010000004").Step);

        await Send("08010000004", "cancel");

        Assert.Contains("Cancelled", _sender.LastBody);
        Assert.Equal(ConversationStep.Idle, State("08010000004").Step);
    }

    [Fact]
    public async Task StepByStep_FullFlow_CreatesOrder()
    {
        const string phone = "08010000005";
        _parser.NextParsed = new ParsedOrder("", "", "",
            new List<ParsedItem> { new("Sneakers", 2, 22500) }, 45000);
        _mediator.OnCreateOrder = cmd => Result<OrderDto>.Success(new OrderDto(
            Guid.NewGuid(), cmd.VendorPhone, cmd.CustomerName, cmd.CustomerPhone,
            cmd.DeliveryAddress, new(), 4500000, "NGN", OrderStatus.AwaitingPayment,
            "ref-1", "https://pay.test/ref-1", null, null, null));

        await Send(phone, "1");
        await Send(phone, "Chidi");
        Assert.Equal(ConversationStep.DraftCustomerPhone, State(phone).Step);
        await Send(phone, "08087654321");
        Assert.Equal(ConversationStep.DraftAddress, State(phone).Step);
        await Send(phone, "Lekki Phase 1");
        Assert.Equal(ConversationStep.DraftItems, State(phone).Step);
        await Send(phone, "2x Sneakers @22500");
        Assert.Equal(ConversationStep.Confirming, State(phone).Step);
        Assert.Contains("Please confirm", _sender.LastBody);

        await Send(phone, "YES");

        Assert.Contains("Payment link", _sender.LastBody);
        Assert.Equal(ConversationStep.Idle, State(phone).Step);
        Assert.Contains(_vendors.Vendors, v => v.Phone == phone);
    }

    [Fact]
    public async Task PartialFreeTextOrder_AsksForMissingFields()
    {
        const string phone = "08010000006";
        _parser.NextIntent = new ChatIntent(ChatIntentKind.CreateOrder, null, null);
        _parser.NextParsed = new ParsedOrder("", "", "",
            new List<ParsedItem> { new("Sneakers", 2, 22500) }, 45000);

        await Send(phone, "2 sneakers for 45000");

        Assert.Contains("customer name", _sender.LastBody);
        Assert.Equal(ConversationStep.DraftCustomerName, State(phone).Step);
    }

    [Fact]
    public async Task ExpiredState_ResetsWithNotice()
    {
        const string phone = "08010000007";
        await Send(phone, "1");
        var state = State(phone);
        typeof(InstaSafe.Domain.Common.BaseEntity).GetProperty("UpdatedAt")!
            .GetSetMethod(true)!
            .Invoke(state, new object?[] { DateTimeOffset.UtcNow.AddHours(-1) });

        await Send(phone, "Chidi");

        Assert.Contains("reset our chat", _sender.LastBody);
        Assert.Equal(ConversationStep.AwaitingMenuChoice, State(phone).Step);
    }

    [Fact]
    public async Task TrackUnknownRef_RepliesNotFound()
    {
        const string phone = "08010000008";
        await Send(phone, "2");
        Assert.Equal(ConversationStep.AwaitingTrackRef, State(phone).Step);

        await Send(phone, "ref-nope");

        Assert.Contains("couldn't find", _sender.LastBody);
    }

    [Fact]
    public async Task InvalidPhone_Reasks()
    {
        const string phone = "08010000009";
        await Send(phone, "1");
        await Send(phone, "Chidi");

        await Send(phone, "abc");

        Assert.Contains("doesn't look like a phone number", _sender.LastBody);
        Assert.Equal(ConversationStep.DraftCustomerPhone, State(phone).Step);
    }

    [Fact]
    public async Task Reply_UsesSenderJid_NotReconstructedCus()
    {
        SeedVendor("08010000010");
        await _router.RouteAsync("08010000010", "Hi", "91745383633143@lid", CancellationToken.None);

        Assert.Equal("91745383633143@lid", _sender.Sent[^1].To);
    }

    [Fact]
    public async Task Gate_UnknownPhone_GetsSignupNudge_AndNoState()
    {
        _parser.NextIntent = new ChatIntent(ChatIntentKind.Greeting, null, null);

        var handled = await _router.RouteAsync("08019990001", "Hi", null, CancellationToken.None);

        Assert.True(handled);
        Assert.Contains("not signed up", _sender.LastBody);
        Assert.Contains("https://app.test/signup", _sender.LastBody);
        Assert.False(_states.Store.ContainsKey("08019990001"));
    }

    [Fact]
    public async Task Gate_UnverifiedVendor_GetsSignupNudge()
    {
        _vendors.Vendors.Add(new Vendor
        {
            Phone = "08019990002",
            DisplayName = "Unverified",
            EmailVerified = false,
            OnboardingCompleted = false
        });

        await _router.RouteAsync("08019990002", "Hi", null, CancellationToken.None);

        Assert.Contains("not signed up", _sender.LastBody);
    }

    [Fact]
    public async Task Gate_DeactivatedVendor_Blocked()
    {
        _vendors.Vendors.Add(new Vendor
        {
            Phone = "08019990003",
            DisplayName = "Gone",
            EmailVerified = true,
            OnboardingCompleted = true,
            IsActive = false
        });

        await _router.RouteAsync("08019990003", "Hi", null, CancellationToken.None);

        Assert.Contains("deactivated", _sender.LastBody);
    }

    [Fact]
    public async Task Gate_UnonboardedVendor_GetsOnboardingNudge()
    {
        _vendors.Vendors.Add(new Vendor
        {
            Phone = "08019990004",
            DisplayName = "NoBank",
            EmailVerified = true,
            OnboardingCompleted = false
        });

        await _router.RouteAsync("08019990004", "1", null, CancellationToken.None);

        Assert.Contains("payout", _sender.LastBody);
        Assert.Contains("https://app.test/onboarding", _sender.LastBody);
    }

    [Fact]
    public void Draft_RoundTripsThroughJson()
    {
        var draft = new OrderDraft("Chidi", "0801", "Lekki",
            new List<DraftItem> { new("Sneakers", 2, 22500) }, 45000);
        var loaded = OrderDraft.Load(draft.Save());
        Assert.True(loaded.IsComplete());
        Assert.Equal("Chidi", loaded.CustomerName);
        Assert.Single(loaded.Items);
    }
}
