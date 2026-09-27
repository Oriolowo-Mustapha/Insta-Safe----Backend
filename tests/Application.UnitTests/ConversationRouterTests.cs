using InstaSafe.Application.Common.Helpers;
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
        public List<SavedOrderDraft> Drafts { get; } = new();
        public Task<ConversationState?> GetByPhoneAsync(string phone, CancellationToken ct)
            => Task.FromResult(Store.TryGetValue(phone, out var s) ? s : null);
        public Task AddAsync(ConversationState state, CancellationToken ct)
        {
            Store[state.Phone] = state;
            return Task.CompletedTask;
        }
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<List<SavedOrderDraft>> ListDraftsAsync(string phone, DraftTicketStatus status, CancellationToken ct)
            => Task.FromResult(Drafts.Where(d => d.VendorPhone == phone && d.Status == status).ToList());
        public Task<SavedOrderDraft?> GetDraftAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Drafts.FirstOrDefault(d => d.Id == id));
        public Task AddDraftAsync(SavedOrderDraft draft, CancellationToken ct)
        {
            Drafts.Add(draft);
            return Task.CompletedTask;
        }
        public Task RemoveDraftAsync(SavedOrderDraft draft, CancellationToken ct)
        {
            Drafts.Remove(draft);
            return Task.CompletedTask;
        }
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
            => Task.FromResult(Orders.Where(o => o.VendorId == vendorId || o.VendorPhone == vendorPhone).ToList());
        public Task<List<Order>> ListByDriverAsync(Guid driverId, string driverPhone, int page, int pageSize, CancellationToken ct)
            => Task.FromResult(Orders.Where(o => o.DriverId == driverId || o.DriverPhone == driverPhone).ToList());
        public Task<List<Order>> ListUnpaidByEmailAsync(string email, CancellationToken ct)
            => Task.FromResult(Orders.Where(o => o.BuyerEmail == email).ToList());
    }

    private sealed class FakeParser : IGroqParser
    {
        public ChatIntent NextIntent { get; set; } = new(ChatIntentKind.Unknown, null, null);
        public ParsedOrder NextParsed { get; set; } = new("", "", "", new List<ParsedItem>(), 0);
        public string NextChatReply { get; set; } = "You are most welcome! 🎉";
        public bool FailChatReply { get; set; }
        public Task<ChatIntent> ClassifyIntentAsync(string rawText, CancellationToken ct)
            => Task.FromResult(NextIntent);
        public Task<ParsedOrder> ParseOrderTextAsync(string rawText, CancellationToken ct)
            => Task.FromResult(NextParsed);
        public Task<string> ChatReplyAsync(string rawText, CancellationToken ct)
            => FailChatReply ? throw new HttpRequestException("groq down") : Task.FromResult(NextChatReply);
    }

    private sealed class FakeSender : IWhatsAppSender
    {
        public List<(string To, string Body)> Sent { get; } = new();
        public string LastBody => Sent.Count == 0 ? "" : Sent[^1].Body;
        public bool FailNextSend { get; set; }
        public Task SendTextAsync(string toPhone, string body, CancellationToken ct)
        {
            if (FailNextSend)
            {
                FailNextSend = false;
                throw new HttpRequestException("openwa down");
            }
            Sent.Add((toPhone, body));
            return Task.CompletedTask;
        }
        public Task SendTemplateAsync(string toPhone, string templateName, Dictionary<string, string> vars, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class FakeMediator : IMediator
    {
        public Func<CreateOrderCommand, Result<OrderDto>>? OnCreateOrder;
        public Func<Guid, Result<OrderDto>>? OnGetOrderById;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is CreateOrderCommand cmd && OnCreateOrder is not null)
                return Task.FromResult((TResponse)(object)OnCreateOrder(cmd));
            if (request is InstaSafe.Application.Features.Orders.Queries.GetOrderById.GetOrderByIdQuery q
                && OnGetOrderById is not null)
                return Task.FromResult((TResponse)(object)OnGetOrderById(q.OrderId));
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

    private sealed class FakePaystack : IPaystackClient
    {
        public List<BankInfo> Banks { get; set; } = new()
        {
            new("Guaranty Trust Bank", "guaranty-trust-bank", "058"),
            new("Access Bank", "access-bank", "044"),
            new("Wema Bank", "wema-bank", "035")
        };
        public string? HolderName { get; set; } = "Musa Rider";
        public Task<(string Reference, string AuthUrl)> InitializeTransactionAsync(
            string email, long amountKobo, Guid orderId, CancellationToken ct)
            => Task.FromResult(("ref", "https://pay.test"));
        public Task<bool> VerifyTransactionAsync(string reference, CancellationToken ct)
            => Task.FromResult(true);
        public Task<string?> CreateRecipientAsync(string accountNumber, string bankCode, string name, CancellationToken ct)
            => Task.FromResult<string?>("RCP_TEST");
        public Task<string?> InitiateTransferAsync(long amountKobo, string recipientCode, string reason, CancellationToken ct)
            => Task.FromResult<string?>(null);
        public Task<(bool Success, string? RefundReference, string? Error)> RefundTransactionAsync(string reference, CancellationToken ct)
            => Task.FromResult((true, (string?)"RFND_TEST", (string?)null));
        public Task<(bool Success, string? CustomerCode, string? Error)> CreateCustomerAsync(string email, string firstName, string lastName, string phone, Guid orderId, CancellationToken ct)
            => Task.FromResult((true, (string?)"CUS_TEST", (string?)null));
        public Task<(bool Success, string? AccountNumber, string? AccountName, string? Bank, string? Error)> AssignDedicatedAccountAsync(string customerCode, string? preferredBank, CancellationToken ct)
            => Task.FromResult((true, (string?)"0123456789", (string?)"Ada Obi", (string?)"Wema", (string?)null));
        public Task<List<(string Name, string Slug, string Code)>> ListTransferBanksAsync(CancellationToken ct)
            => Task.FromResult(new List<(string Name, string Slug, string Code)>());
        public Task<List<(string Name, string Slug, string Code)>> ListAllBanksAsync(CancellationToken ct)
            => Task.FromResult(Banks.Select(b => (b.Name, b.Slug, b.Code)).ToList());
        public Task<AccountResolveResult> ResolveAccountAsync(
            string accountNumber, string bankCode, CancellationToken ct)
            => Task.FromResult(NextFailureKind == ResolveFailureKind.Unavailable
                ? new AccountResolveResult(false, null, ResolveFailureKind.Unavailable, "service down")
                : HolderName is null
                    ? new AccountResolveResult(false, null, ResolveFailureKind.Invalid, "bad account")
                    : new AccountResolveResult(true, HolderName, ResolveFailureKind.Invalid, ""));
        public ResolveFailureKind NextFailureKind { get; set; } = ResolveFailureKind.Invalid;
    }

    private readonly FakeStates _states = new();
    private readonly FakeVendors _vendors = new();
    private readonly FakeOrders _orders = new();
    private readonly FakeParser _parser = new();
    private readonly FakeSender _sender = new();
    private readonly FakeMediator _mediator = new();
    private readonly FakePaystack _paystack = new();
    private readonly ConversationRouter _router;

    public ConversationRouterTests()
    {
        var banks = new BankDirectory(
            _paystack, NullLogger<BankDirectory>.Instance);
        _router = new ConversationRouter(
            _states, _vendors, _orders, _parser, _sender, _mediator,
            new PassThroughSanitizer(), banks,
            _paystack, NullLogger<ConversationRouter>.Instance);
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

    private ConversationState State(string phone) =>
        _states.Store[InstaSafe.Application.Common.Helpers.PhoneNormalizer.Normalize(phone)];

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
        await Send(phone, "chidi@example.com");
        Assert.Equal(ConversationStep.DraftAddress, State(phone).Step);
        await Send(phone, "Lekki Phase 1");
        Assert.Equal(ConversationStep.DraftItems, State(phone).Step);
        await Send(phone, "2x Sneakers @22500");
        Assert.Equal(ConversationStep.DraftFulfillment, State(phone).Step);
        await Send(phone, "1");
        Assert.Equal(ConversationStep.DraftDeliveryFee, State(phone).Step);
        await Send(phone, "0");
        Assert.Equal(ConversationStep.Confirming, State(phone).Step);
        Assert.Contains("Please confirm", _sender.LastBody);

        await Send(phone, "YES");

        Assert.Contains("Order created", _sender.LastBody);
        Assert.Equal(ConversationStep.Idle, State(phone).Step);
        Assert.Contains(_vendors.Vendors, v =>
            v.Phone == InstaSafe.Application.Common.Helpers.PhoneNormalizer.Normalize(phone));
    }

    [Fact]
    public async Task StepByStep_DispatchFlow_CollectsFeeAndDriver()
    {
        const string phone = "08010000010";
        _parser.NextParsed = new ParsedOrder("", "", "",
            new List<ParsedItem> { new("Sneakers", 2, 22500) }, 45000);
        CreateOrderCommand? captured = null;
        _mediator.OnCreateOrder = cmd =>
        {
            captured = cmd;
            return Result<OrderDto>.Success(new OrderDto(
                Guid.NewGuid(), cmd.VendorPhone, cmd.CustomerName, cmd.CustomerPhone,
                cmd.DeliveryAddress, new(), 5000000, "NGN", OrderStatus.AwaitingPayment,
                "ref-1", "https://pay.test/ref-1", null, null, null));
        };

        await Send(phone, "1");
        await Send(phone, "Chidi");
        await Send(phone, "08087654321");
        await Send(phone, "chidi@example.com");
        await Send(phone, "Lekki Phase 1");
        await Send(phone, "2x Sneakers @22500");
        Assert.Equal(ConversationStep.DraftFulfillment, State(phone).Step);
        await Send(phone, "1");
        Assert.Equal(ConversationStep.DraftDeliveryFee, State(phone).Step);

        await Send(phone, "5000");
        Assert.Equal(ConversationStep.DraftDriverPhone, State(phone).Step);
        await Send(phone, "08055556666");
        Assert.Equal(ConversationStep.DraftDriverAccount, State(phone).Step);
        await Send(phone, "0123456789");
        Assert.Equal(ConversationStep.DraftDriverBankName, State(phone).Step);
        await Send(phone, "GTBank");
        Assert.Equal(ConversationStep.DraftDriverConfirm, State(phone).Step);
        Assert.Contains("Guaranty Trust Bank", _sender.LastBody);
        Assert.Contains("Musa Rider", _sender.LastBody);
        await Send(phone, "YES");
        Assert.Equal(ConversationStep.Confirming, State(phone).Step);
        var summary = _sender.LastBody.Replace(",", "");
        Assert.Contains("5000", summary);
        Assert.Contains("50000", summary);

        await Send(phone, "YES");

        Assert.NotNull(captured);
        Assert.Equal(5000, captured!.DeliveryFeeNgn);
        Assert.Equal("2348055556666", captured.DriverPhone);
        Assert.Equal("0123456789", captured.DriverAccountNumber);
        Assert.Equal("058", captured.DriverBankCode);
        Assert.Contains("Order created", _sender.LastBody);
    }

    [Fact]
    public async Task StepByStep_SkipDriver_ClearsFee()
    {
        const string phone = "08010000011";
        _parser.NextParsed = new ParsedOrder("", "", "",
            new List<ParsedItem> { new("Sneakers", 2, 22500) }, 45000);
        _mediator.OnCreateOrder = cmd => Result<OrderDto>.Success(new OrderDto(
            Guid.NewGuid(), cmd.VendorPhone, cmd.CustomerName, cmd.CustomerPhone,
            cmd.DeliveryAddress, new(), 4500000, "NGN", OrderStatus.AwaitingPayment,
            "ref-1", "https://pay.test/ref-1", null, null, null));

        await Send(phone, "1");
        await Send(phone, "Chidi");
        await Send(phone, "08087654321");
        await Send(phone, "chidi@example.com");
        await Send(phone, "Lekki");
        await Send(phone, "2x Sneakers @22500");
        Assert.Equal(ConversationStep.DraftFulfillment, State(phone).Step);
        await Send(phone, "1");
        await Send(phone, "5000");
        await Send(phone, "skip");
        Assert.Equal(ConversationStep.Confirming, State(phone).Step);

        await Send(phone, "YES");
        Assert.Contains("Order created", _sender.LastBody);
    }

    [Fact]
    public async Task Confirm_PaystackFailure_RepliesInsteadOfSilence()
    {
        const string phone = "08010000013";
        _parser.NextParsed = new ParsedOrder("", "", "",
            new List<ParsedItem> { new("Sneakers", 2, 22500) }, 45000);
        _mediator.OnCreateOrder = _ => throw new HttpRequestException("Paystack down");

        await Send(phone, "1");
        await Send(phone, "Chidi");
        await Send(phone, "08087654321");
        await Send(phone, "chidi@example.com");
        await Send(phone, "Lekki");
        await Send(phone, "2x Sneakers @22500");
        Assert.Equal(ConversationStep.DraftFulfillment, State(phone).Step);
        await Send(phone, "1");
        await Send(phone, "0");
        Assert.Equal(ConversationStep.Confirming, State(phone).Step);

        await Send(phone, "YES");

        Assert.Contains("Something went wrong", _sender.LastBody);
        Assert.Equal(ConversationStep.Idle, State(phone).Step);
    }

    [Fact]
    public async Task Confirm_SendFailure_RetryResends_WithoutDuplicate()
    {
        const string phone = "08010000050";
        _parser.NextParsed = new ParsedOrder("", "", "",
            new List<ParsedItem> { new("Sneakers", 2, 22500) }, 45000);
        var orderId = Guid.NewGuid();
        var createCalls = 0;
        _mediator.OnCreateOrder = cmd =>
        {
            createCalls++;
            return Result<OrderDto>.Success(new OrderDto(
                orderId, cmd.VendorPhone, cmd.CustomerName, cmd.CustomerPhone,
                cmd.DeliveryAddress, new(), 4500000, "NGN", OrderStatus.AwaitingPayment,
                "ref-1", "https://pay.test/ref-1", null, null, null));
        };
        _mediator.OnGetOrderById = id => id == orderId
            ? Result<OrderDto>.Success(new OrderDto(
                orderId, phone, "Chidi", "08087654321",
                "Lekki", new(), 4500000, "NGN", OrderStatus.AwaitingPayment,
                "ref-1", "https://pay.test/ref-1", null, null, null))
            : Result<OrderDto>.Failure("not found");

        await Send(phone, "1");
        await Send(phone, "Chidi");
        await Send(phone, "08087654321");
        await Send(phone, "chidi@example.com");
        await Send(phone, "Lekki");
        await Send(phone, "2x Sneakers @22500");
        await Send(phone, "1");
        await Send(phone, "0");

        _sender.FailNextSend = true;
        await Send(phone, "YES");

        Assert.Equal(1, createCalls);
        Assert.Equal(ConversationStep.Confirming, State(phone).Step);

        await Send(phone, "YES");

        Assert.Equal(1, createCalls);
        Assert.Contains("no duplicate", _sender.LastBody);
        Assert.Equal(ConversationStep.Idle, State(phone).Step);
    }

    [Fact]
    public async Task StepByStep_UnknownBank_Reasks()
    {
        const string phone = "08010000012";
        _parser.NextParsed = new ParsedOrder("", "", "",
            new List<ParsedItem> { new("Sneakers", 2, 22500) }, 45000,
            5000, "08055556666");
        _mediator.OnCreateOrder = cmd => Result<OrderDto>.Success(new OrderDto(
            Guid.NewGuid(), cmd.VendorPhone, cmd.CustomerName, cmd.CustomerPhone,
            cmd.DeliveryAddress, new(), 5000000, "NGN", OrderStatus.AwaitingPayment,
            "ref-1", "https://pay.test/ref-1", null, null, null));

        await Send(phone, "1");
        await Send(phone, "Chidi");
        await Send(phone, "08087654321");
        await Send(phone, "chidi@example.com");
        await Send(phone, "Lekki");
        await Send(phone, "2x Sneakers @22500");
        Assert.Equal(ConversationStep.DraftFulfillment, State(phone).Step);
        await Send(phone, "1");
        Assert.Equal(ConversationStep.DraftDeliveryFee, State(phone).Step);
        await Send(phone, "5000");
        Assert.Equal(ConversationStep.DraftDriverPhone, State(phone).Step);
        await Send(phone, "08055556666");
        Assert.Equal(ConversationStep.DraftDriverAccount, State(phone).Step);
        await Send(phone, "0123456789");
        await Send(phone, "Bank of Nowhere");
        Assert.Equal(ConversationStep.DraftDriverBankName, State(phone).Step);
        Assert.Contains("couldn't find that bank", _sender.LastBody);
        await Send(phone, "GTB");
        Assert.Equal(ConversationStep.DraftDriverConfirm, State(phone).Step);
    }

    [Fact]
    public async Task StepByStep_InvalidAccount_BackToAccount()
    {
        const string phone = "08010000014";
        _parser.NextParsed = new ParsedOrder("", "", "",
            new List<ParsedItem> { new("Sneakers", 2, 22500) }, 45000);
        _paystack.HolderName = null;
        _paystack.NextFailureKind = ResolveFailureKind.Invalid;

        await Send(phone, "1");
        await Send(phone, "Chidi");
        await Send(phone, "08087654321");
        await Send(phone, "chidi@example.com");
        await Send(phone, "Lekki");
        await Send(phone, "2x Sneakers @22500");
        Assert.Equal(ConversationStep.DraftFulfillment, State(phone).Step);
        await Send(phone, "1");
        await Send(phone, "5000");
        await Send(phone, "08055556666");
        await Send(phone, "0123456789");
        await Send(phone, "GTBank");
        Assert.Equal(ConversationStep.DraftDriverAccount, State(phone).Step);
        Assert.Contains("couldn't verify", _sender.LastBody);
    }

    [Fact]
    public async Task StepByStep_ServiceDown_ContinuesWithSelfCheck()
    {
        const string phone = "08010000015";
        _parser.NextParsed = new ParsedOrder("", "", "",
            new List<ParsedItem> { new("Sneakers", 2, 22500) }, 45000);
        _paystack.NextFailureKind = ResolveFailureKind.Unavailable;
        _mediator.OnCreateOrder = cmd => Result<OrderDto>.Success(new OrderDto(
            Guid.NewGuid(), cmd.VendorPhone, cmd.CustomerName, cmd.CustomerPhone,
            cmd.DeliveryAddress, new(), 5000000, "NGN", OrderStatus.AwaitingPayment,
            "ref-1", "https://pay.test/ref-1", null, null, null));

        await Send(phone, "1");
        await Send(phone, "Chidi");
        await Send(phone, "08087654321");
        await Send(phone, "chidi@example.com");
        await Send(phone, "Lekki");
        await Send(phone, "2x Sneakers @22500");
        Assert.Equal(ConversationStep.DraftFulfillment, State(phone).Step);
        await Send(phone, "1");
        await Send(phone, "5000");
        await Send(phone, "08055556666");
        await Send(phone, "0123456789");
        await Send(phone, "GTBank");

        Assert.Equal(ConversationStep.Confirming, State(phone).Step);
        Assert.Contains("temporarily unavailable", _sender.Sent[^2].Body);
        Assert.Contains("unverified", _sender.LastBody);

        await Send(phone, "YES");
        Assert.Contains("Order created", _sender.LastBody);
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
        _parser.NextIntent = new ChatIntent(ChatIntentKind.TrackOrder, null, "ref-nope");
        await Send(phone, "2");
        Assert.Equal(ConversationStep.AwaitingTrackRef, State(phone).Step);

        await Send(phone, "ref-nope");

        Assert.Contains("couldn't find", _sender.LastBody);
    }

    [Fact]
    public async Task TrackStep_ValidRef_TracksWithoutAI()
    {
        const string phone = "08010000060";
        SeedVendor(phone);
        _orders.Orders.Add(new Order
        {
            OrderNumber = "IS-ABCDEF",
            VendorPhone = InstaSafe.Application.Common.Helpers.PhoneNormalizer.Normalize(phone),
            CustomerName = "Chidi",
            CustomerPhone = "0802",
            DeliveryAddress = "Lekki",
            AmountKobo = 4500000,
            Status = OrderStatus.Held
        });
        _parser.NextIntent = new ChatIntent(ChatIntentKind.Unknown, null, null);
        await Send(phone, "2");

        await Send(phone, "is-abcdef");

        Assert.Contains("IS-ABCDEF", _sender.LastBody);
        Assert.Contains("Held", _sender.LastBody);
        Assert.Equal(ConversationStep.Idle, State(phone).Step);
    }

    [Fact]
    public async Task TrackStep_FreeText_ListsMyOrders()
    {
        const string phone = "08010000061";
        SeedVendor(phone);
        var norm = InstaSafe.Application.Common.Helpers.PhoneNormalizer.Normalize(phone);
        _orders.Orders.Add(new Order
        {
            OrderNumber = "IS-111111", VendorPhone = norm, CustomerName = "A",
            CustomerPhone = "0802", DeliveryAddress = "X", AmountKobo = 100000,
            Status = OrderStatus.Held
        });
        _orders.Orders.Add(new Order
        {
            OrderNumber = "IS-222222", VendorPhone = norm, CustomerName = "B",
            CustomerPhone = "0803", DeliveryAddress = "Y", AmountKobo = 200000,
            Status = OrderStatus.AwaitingPayment
        });
        _orders.Orders.Add(new Order
        {
            OrderNumber = "IS-999999", VendorPhone = "2348099999999", CustomerName = "Stranger",
            CustomerPhone = "0804", DeliveryAddress = "Z", AmountKobo = 300000,
            Status = OrderStatus.Held
        });
        _parser.NextIntent = new ChatIntent(ChatIntentKind.ListOrders, null, null);
        await Send(phone, "2");

        await Send(phone, "i dunno the order refrece just list all the orders i have");

        Assert.Contains("IS-111111", _sender.LastBody);
        Assert.Contains("IS-222222", _sender.LastBody);
        Assert.DoesNotContain("IS-999999", _sender.LastBody);
        Assert.Equal(ConversationStep.AwaitingTrackRef, State(phone).Step);
    }

    [Fact]
    public async Task Menu_ListOrdersIntent_Lists()
    {
        const string phone = "08010000062";
        SeedVendor(phone);
        var norm = InstaSafe.Application.Common.Helpers.PhoneNormalizer.Normalize(phone);
        _orders.Orders.Add(new Order
        {
            OrderNumber = "IS-333333", VendorPhone = norm, CustomerName = "C",
            CustomerPhone = "0805", DeliveryAddress = "W", AmountKobo = 50000,
            Status = OrderStatus.Released
        });
        _parser.NextIntent = new ChatIntent(ChatIntentKind.ListOrders, null, null);

        await Send(phone, "show me my orders");

        Assert.Contains("IS-333333", _sender.LastBody);
        Assert.Contains("Released", _sender.LastBody);
    }

    [Fact]
    public async Task Menu_Chitchat_GetsWarmReply_KeepsStep()
    {
        const string phone = "08010000063";
        SeedVendor(phone);
        _parser.NextIntent = new ChatIntent(ChatIntentKind.Chitchat, null, null);
        _parser.NextChatReply = "You are most welcome! 🎉 Anything else?";

        await Send(phone, "thanks dear");

        Assert.Contains("welcome", _sender.LastBody);
    }

    [Fact]
    public async Task Menu_ChitchatFailure_FallsBack()
    {
        const string phone = "08010000064";
        SeedVendor(phone);
        _parser.NextIntent = new ChatIntent(ChatIntentKind.Chitchat, null, null);
        _parser.FailChatReply = true;

        await Send(phone, "lol");

        Assert.Contains("MENU", _sender.LastBody);
    }

    [Fact]
    public async Task Cancel_MidDraft_PreservesTicket()
    {
        const string phone = "08010000065";
        await Send(phone, "1");
        await Send(phone, "Chidi");

        await Send(phone, "CANCEL");

        Assert.Contains("Cancelled", _sender.LastBody);
        Assert.Single(_states.Drafts.Where(d => d.Status == DraftTicketStatus.Open));
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
    public async Task Back_ReturnsToPreviousAnsweredStep()
    {
        const string phone = "08010000032";
        await Send(phone, "1");
        await Send(phone, "Chidi");
        await Send(phone, "08087654321");
        await Send(phone, "chidi@example.com");
        Assert.Equal(ConversationStep.DraftAddress, State(phone).Step);

        await Send(phone, "BACK");

        Assert.Equal(ConversationStep.DraftBuyerEmail, State(phone).Step);
        Assert.Contains("buyer email", _sender.LastBody);

        await Send(phone, "other@example.com");
        Assert.Equal(ConversationStep.DraftAddress, State(phone).Step);
    }

    [Fact]
    public async Task Back_AtFirstStep_Nudges()
    {
        const string phone = "08010000033";
        await Send(phone, "1");
        Assert.Equal(ConversationStep.DraftCustomerName, State(phone).Step);

        await Send(phone, "BACK");

        Assert.Equal(ConversationStep.DraftCustomerName, State(phone).Step);
        Assert.Contains("Nothing to go back to", _sender.LastBody);
    }

    [Fact]
    public async Task Fulfillment_Digital_SkipsFeeAndDriver()
    {
        const string phone = "08010000040";
        _parser.NextParsed = new ParsedOrder("", "", "",
            new List<ParsedItem> { new("Ebook", 1, 5000) }, 5000);
        CreateOrderCommand? captured = null;
        _mediator.OnCreateOrder = cmd =>
        {
            captured = cmd;
            return Result<OrderDto>.Success(new OrderDto(
                Guid.NewGuid(), cmd.VendorPhone, cmd.CustomerName, cmd.CustomerPhone,
                cmd.DeliveryAddress, new(), 5000000, "NGN", OrderStatus.AwaitingPayment,
                "ref-1", "https://pay.test/ref-1", null, null, null));
        };

        await Send(phone, "1");
        await Send(phone, "Chidi");
        await Send(phone, "08087654321");
        await Send(phone, "chidi@example.com");
        await Send(phone, "Email delivery");
        await Send(phone, "1x Ebook @5000");
        Assert.Equal(ConversationStep.DraftFulfillment, State(phone).Step);
        Assert.Contains("dispatch rider", _sender.LastBody);

        await Send(phone, "2");
        Assert.Equal(ConversationStep.Confirming, State(phone).Step);
        Assert.Contains("digital", _sender.LastBody);

        await Send(phone, "YES");

        Assert.NotNull(captured);
        Assert.Equal(FulfillmentType.Digital, captured!.Fulfillment);
        Assert.Equal(0, captured.DeliveryFeeNgn);
        Assert.Contains("Order created", _sender.LastBody);
    }

    [Fact]
    public async Task Fulfillment_Garbage_Reasks()
    {
        const string phone = "08010000041";
        _parser.NextParsed = new ParsedOrder("", "", "",
            new List<ParsedItem> { new("Ebook", 1, 5000) }, 5000);

        await Send(phone, "1");
        await Send(phone, "Chidi");
        await Send(phone, "08087654321");
        await Send(phone, "chidi@example.com");
        await Send(phone, "Email delivery");
        await Send(phone, "1x Ebook @5000");

        await Send(phone, "maybe");
        Assert.Equal(ConversationStep.DraftFulfillment, State(phone).Step);
    }

    [Fact]
    public async Task FreeText_CompleteDigitalOrder_GoesStraightToConfirm()
    {
        const string phone = "08010000042";
        _parser.NextIntent = new ChatIntent(ChatIntentKind.CreateOrder, null, null);
        _parser.NextParsed = new ParsedOrder("Chidi", "2348028613918", "Email delivery",
            new List<ParsedItem> { new("Ebook", 1, 5000) }, 5000,
            0, "", "chidi@example.com", "digital");

        await Send(phone, "Ebook for Chidi 0808028613918 chidi@example.com email delivery 5000 digital");

        Assert.Equal(ConversationStep.Confirming, State(phone).Step);
        Assert.Contains("digital", _sender.LastBody);
    }

    [Fact]
    public async Task FreeText_PartialDigitalOrder_AsksOnlyMissing()
    {
        const string phone = "08010000043";
        _parser.NextIntent = new ChatIntent(ChatIntentKind.CreateOrder, null, null);
        _parser.NextParsed = new ParsedOrder("Chidi", "2348028613918", "",
            new List<ParsedItem> { new("Ebook", 1, 5000) }, 5000,
            0, "", "chidi@example.com", "digital");

        await Send(phone, "Ebook for Chidi, digital");

        Assert.Equal(ConversationStep.DraftAddress, State(phone).Step);
    }

    [Fact]
    public async Task Reply_UsesSenderJid_NotReconstructedCus()
    {
        SeedVendor("08010000010");
        await _router.RouteAsync("08010000010", "Hi", "91745383633143@lid", CancellationToken.None);

        Assert.Equal("91745383633143@lid", _sender.Sent[^1].To);
    }

    [Fact]
    public async Task Gate_UnknownPhone_StaysSilent_AndNoState()
    {
        _parser.NextIntent = new ChatIntent(ChatIntentKind.Greeting, null, null);

        var handled = await _router.RouteAsync("08019990001", "Hi", null, CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(_sender.Sent);
        Assert.False(_states.Store.ContainsKey("08019990001"));
    }

    [Fact]
    public async Task Gate_UnverifiedVendor_StaysSilent()
    {
        _vendors.Vendors.Add(new Vendor
        {
            Phone = "2348019990002",
            DisplayName = "Unverified",
            EmailVerified = false,
            OnboardingCompleted = false
        });

        await _router.RouteAsync("08019990002", "Hi", null, CancellationToken.None);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Gate_DeactivatedVendor_StaysSilent()
    {
        _vendors.Vendors.Add(new Vendor
        {
            Phone = "2348019990003",
            DisplayName = "Gone",
            EmailVerified = true,
            OnboardingCompleted = true,
            IsActive = false
        });

        await _router.RouteAsync("08019990003", "Hi", null, CancellationToken.None);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Gate_UnonboardedVendor_StaysSilent()
    {
        _vendors.Vendors.Add(new Vendor
        {
            Phone = "2348019990004",
            DisplayName = "NoBank",
            EmailVerified = true,
            OnboardingCompleted = false
        });

        await _router.RouteAsync("08019990004", "1", null, CancellationToken.None);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public void Draft_RoundTripsThroughJson()
    {
        var draft = new OrderDraft("Chidi", "0801", "Lekki",
            new List<DraftItem> { new("Sneakers", 2, 22500) }, 45000,
            BuyerEmail: "chidi@example.com", Fulfillment: FulfillmentType.Dispatch);
        var loaded = OrderDraft.Load(draft.Save());
        Assert.True(loaded.IsComplete());
        Assert.Equal("Chidi", loaded.CustomerName);
        Assert.Single(loaded.Items);
    }

    private async Task WalkToAddressStep(string phone)
    {
        await Send(phone, "1");
        await Send(phone, "Chidi");
        await Send(phone, "08087654321");
        Assert.Equal(ConversationStep.DraftBuyerEmail, State(phone).Step);
        await Send(phone, "chidi@example.com");
        Assert.Equal(ConversationStep.DraftAddress, State(phone).Step);
    }

    [Fact]
    public async Task Resume_OnlyEmailMissing_GoesStraightToConfirm()
    {
        const string phone = "08010000031";
        SeedVendor(phone);
        var draft = new OrderDraft("Chidi", "2348028613918", "No 15 Ajangbohun",
            new List<DraftItem> { new("Abaya", 2, 40000) }, 83000, 3000,
            "2347088201223", "1043626025", "058", "Guaranty Trust Bank", "Musa Rider", "",
            Fulfillment: FulfillmentType.Dispatch);
        _states.Store[InstaSafe.Application.Common.Helpers.PhoneNormalizer.Normalize(phone)] =
            new ConversationState
            {
                Phone = InstaSafe.Application.Common.Helpers.PhoneNormalizer.Normalize(phone),
                Step = ConversationStep.DraftBuyerEmail,
                DraftJson = draft.Save()
            };

        await Send(phone, "chidi@example.com");

        Assert.Equal(ConversationStep.Confirming, State(phone).Step);
        Assert.Contains("Please confirm", _sender.LastBody);
        Assert.DoesNotContain("delivery address", _sender.LastBody);
    }

    [Fact]
    public async Task BuyerEmailStep_RejectsGarbage()
    {
        const string phone = "08010000030";
        await Send(phone, "1");
        await Send(phone, "Chidi");
        await Send(phone, "08087654321");

        await Send(phone, "not-an-email");

        Assert.Equal(ConversationStep.DraftBuyerEmail, State(phone).Step);
        Assert.Contains("email", _sender.LastBody);
        await Send(phone, "chidi@example.com");
        Assert.Equal(ConversationStep.DraftAddress, State(phone).Step);
    }

    [Fact]
    public async Task Menu_Quit_PreservesDraft_AsTicket()
    {
        const string phone = "08010000020";
        await WalkToAddressStep(phone);

        await Send(phone, "MENU");

        Assert.Equal(ConversationStep.AwaitingMenuChoice, State(phone).Step);
        var open = _states.Drafts.Where(d => d.Status == DraftTicketStatus.Open).ToList();
        Assert.Single(open);
        Assert.Contains("Chidi", OrderDraft.Load(open[0].DraftJson).CustomerName);
    }

    [Fact]
    public async Task Menu_ShowsContinueCount_WhenDraftsOpen()
    {
        const string phone = "08010000021";
        _parser.NextIntent = new ChatIntent(ChatIntentKind.Greeting, null, null);
        await WalkToAddressStep(phone);
        await Send(phone, "MENU");

        await Send(phone, "hi");

        Assert.Contains("4", _sender.LastBody);
        Assert.Contains("Continue", _sender.LastBody);
    }

    [Fact]
    public async Task Browse_Select_ResumesAtMissingStep()
    {
        const string phone = "08010000022";
        await WalkToAddressStep(phone);
        await Send(phone, "MENU");

        await Send(phone, "4");
        Assert.Equal(ConversationStep.BrowsingDrafts, State(phone).Step);
        Assert.Contains("Chidi", _sender.LastBody);

        await Send(phone, "1");
        Assert.Equal(ConversationStep.DraftAddress, State(phone).Step);
        Assert.Contains("delivery address", _sender.LastBody);
    }

    [Fact]
    public async Task Browse_Discard_RemovesTicket()
    {
        const string phone = "08010000023";
        await WalkToAddressStep(phone);
        await Send(phone, "MENU");
        await Send(phone, "4");

        await Send(phone, "D1");

        Assert.DoesNotContain(_states.Drafts, d => d.Status == DraftTicketStatus.Open);
        Assert.Equal(ConversationStep.AwaitingMenuChoice, State(phone).Step);
    }

    [Fact]
    public async Task CompleteOrder_MarksTicketCompleted()
    {
        const string phone = "08010000024";
        _parser.NextParsed = new ParsedOrder("", "", "",
            new List<ParsedItem> { new("Sneakers", 2, 22500) }, 45000);
        var orderId = Guid.NewGuid();
        _mediator.OnCreateOrder = cmd => Result<OrderDto>.Success(new OrderDto(
            orderId, cmd.VendorPhone, cmd.CustomerName, cmd.CustomerPhone,
            cmd.DeliveryAddress, new(), 4500000, "NGN", OrderStatus.AwaitingPayment,
            "ref-1", "https://pay.test/ref-1", null, null, null));
        await WalkToAddressStep(phone);
        await Send(phone, "Lekki");
        await Send(phone, "2x Sneakers @22500");
        Assert.Equal(ConversationStep.DraftFulfillment, State(phone).Step);
        await Send(phone, "1");
        await Send(phone, "0");
        await Send(phone, "YES");

        Assert.Contains("Order created", _sender.LastBody);
        var done = _states.Drafts.Where(d => d.Status == DraftTicketStatus.Completed).ToList();
        Assert.Single(done);
        Assert.Equal(orderId, done[0].CompletedOrderId);
        Assert.DoesNotContain(_states.Drafts, d => d.Status == DraftTicketStatus.Open);
    }

    [Fact]
    public async Task Expiry_PreservesDraft_InsteadOfDeleting()
    {
        const string phone = "08010000025";
        await WalkToAddressStep(phone);
        var state = State(phone);
        typeof(InstaSafe.Domain.Common.BaseEntity).GetProperty("UpdatedAt")!
            .GetSetMethod(true)!
            .Invoke(state, new object?[] { DateTimeOffset.UtcNow.AddHours(-1) });

        await Send(phone, "Chidi");

        Assert.Contains("saved your progress", _sender.LastBody);
        Assert.Single(_states.Drafts, d => d.Status == DraftTicketStatus.Open);
    }
}

