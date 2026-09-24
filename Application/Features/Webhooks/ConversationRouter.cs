using FluentValidation;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Features.Orders.Commands.CreateOrder;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Features.Webhooks;

public class ConversationRouter
{
    public static readonly TimeSpan StateExpiry = TimeSpan.FromMinutes(30);

    private readonly IConversationRepository _states;
    private readonly IVendorRepository _vendors;
    private readonly IOrderRepository _orders;
    private readonly IGroqParser _parser;
    private readonly IWhatsAppSender _sender;
    private readonly IMediator _mediator;
    private readonly ISanitizer _sanitizer;
    private readonly ILogger<ConversationRouter> _logger;

    public ConversationRouter(
        IConversationRepository states, IVendorRepository vendors, IOrderRepository orders,
        IGroqParser parser, IWhatsAppSender sender, IMediator mediator,
        ISanitizer sanitizer, ILogger<ConversationRouter> logger)
    {
        _states = states; _vendors = vendors; _orders = orders;
        _parser = parser; _sender = sender; _mediator = mediator;
        _sanitizer = sanitizer; _logger = logger;
    }

    public async Task<bool> RouteAsync(string rawPhone, string body, string? replyJid, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(rawPhone);
        var replyTo = string.IsNullOrWhiteSpace(replyJid) ? phone : replyJid.Trim();
        var text = (body ?? "").Trim();
        if (string.IsNullOrEmpty(text)) return false;

        var state = await _states.GetByPhoneAsync(phone, ct);
        if (state is null)
        {
            state = new Domain.Entities.ConversationState { Phone = phone };
            await _states.AddAsync(state, ct);
        }
        else if (state.Step != Domain.Entities.ConversationStep.Idle
            && state.UpdatedAt.HasValue
            && DateTimeOffset.UtcNow - state.UpdatedAt.Value > StateExpiry)
        {
            Reset(state);
            await ReplyAndSaveAsync(state, phone, replyTo,
                ConversationTexts.SessionExpired + "\n\n" + ConversationTexts.Menu,
                Domain.Entities.ConversationStep.AwaitingMenuChoice, ct);
            return true;
        }

        await EnsureVendorAsync(phone, ct);

        var upper = text.ToUpperInvariant();
        if (upper is "MENU" or "0")
        {
            Reset(state);
            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.Menu,
                Domain.Entities.ConversationStep.AwaitingMenuChoice, ct);
            return true;
        }
        if (upper is "CANCEL" or "STOP")
        {
            Reset(state);
            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.Cancelled,
                Domain.Entities.ConversationStep.Idle, ct);
            return true;
        }

        var draft = OrderDraft.Load(state.DraftJson);

        switch (state.Step)
        {
            case Domain.Entities.ConversationStep.Idle:
            case Domain.Entities.ConversationStep.AwaitingMenuChoice:
                await HandleMenuOrIntentAsync(state, phone, replyTo, text, draft, ct);
                break;
            case Domain.Entities.ConversationStep.DraftCustomerName:
                if (text.Length > 120) { await ReplyAndSaveAsync(state, phone, replyTo, "That name is too long — please send a shorter name.", state.Step, ct); break; }
                draft = draft with { CustomerName = _sanitizer.Clean(text, 120) };
                await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskCustomerPhone,
                    Domain.Entities.ConversationStep.DraftCustomerPhone, ct, draft);
                break;
            case Domain.Entities.ConversationStep.DraftCustomerPhone:
                var custPhone = PhoneNormalizer.Normalize(text);
                if (custPhone.Length < 7)
                {
                    await ReplyAndSaveAsync(state, phone, replyTo,
                        "That doesn't look like a phone number. Please send the customer's phone number (e.g. 08012345678).",
                        state.Step, ct);
                    break;
                }
                draft = draft with { CustomerPhone = _sanitizer.Clean(custPhone, 20) };
                await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskAddress,
                    Domain.Entities.ConversationStep.DraftAddress, ct, draft);
                break;
            case Domain.Entities.ConversationStep.DraftAddress:
                draft = draft with { Address = _sanitizer.Clean(text, 500) };
                await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskItems,
                    Domain.Entities.ConversationStep.DraftItems, ct, draft);
                break;
            case Domain.Entities.ConversationStep.DraftItems:
                await HandleItemsStepAsync(state, phone, replyTo, text, draft, ct);
                break;
            case Domain.Entities.ConversationStep.DraftAmount:
                await HandleAmountStepAsync(state, phone, replyTo, text, draft, ct);
                break;
            case Domain.Entities.ConversationStep.Confirming:
                await HandleConfirmStepAsync(state, phone, replyTo, text, draft, ct);
                break;
            case Domain.Entities.ConversationStep.AwaitingTrackRef:
                await LookupAndReplyTrackAsync(state, phone, replyTo, text, ct);
                break;
        }

        return true;
    }

    private async Task HandleMenuOrIntentAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string text, OrderDraft draft, CancellationToken ct)
    {
        if (text is "1")
        {
            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskCustomerName,
                Domain.Entities.ConversationStep.DraftCustomerName, ct, OrderDraft.Empty());
            return;
        }
        if (text is "2")
        {
            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskTrackRef,
                Domain.Entities.ConversationStep.AwaitingTrackRef, ct);
            return;
        }
        if (text is "3")
        {
            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.Help,
                Domain.Entities.ConversationStep.Idle, ct);
            return;
        }

        ChatIntent intent;
        try
        {
            intent = await _parser.ClassifyIntentAsync(text, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Intent classification failed, showing menu");
            intent = new ChatIntent(ChatIntentKind.Unknown, null, null);
        }

        switch (intent.Kind)
        {
            case ChatIntentKind.Greeting:
                await ReplyAndSaveAsync(state, phone, replyTo,
                    ConversationTexts.Welcome + "\n\n" + ConversationTexts.Menu,
                    Domain.Entities.ConversationStep.AwaitingMenuChoice, ct);
                break;
            case ChatIntentKind.MenuSelect:
                await HandleMenuOptionAsync(state, phone, replyTo, intent.MenuOption, ct);
                break;
            case ChatIntentKind.CreateOrder:
                await HandleFreeTextOrderAsync(state, phone, replyTo, text, ct);
                break;
            case ChatIntentKind.TrackOrder:
                if (intent.TrackReference is not null)
                    await LookupAndReplyTrackAsync(state, phone, replyTo, intent.TrackReference, ct);
                else
                    await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskTrackRef,
                        Domain.Entities.ConversationStep.AwaitingTrackRef, ct);
                break;
            case ChatIntentKind.Help:
                await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.Help,
                    Domain.Entities.ConversationStep.Idle, ct);
                break;
            case ChatIntentKind.Cancel:
                Reset(state);
                await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.Cancelled,
                    Domain.Entities.ConversationStep.Idle, ct);
                break;
            default:
                await ReplyAndSaveAsync(state, phone, replyTo,
                    "I didn't quite get that. " + ConversationTexts.Menu,
                    Domain.Entities.ConversationStep.AwaitingMenuChoice, ct);
                break;
        }
    }

    private async Task HandleMenuOptionAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, int? option, CancellationToken ct)
    {
        switch (option)
        {
            case 1:
                await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskCustomerName,
                    Domain.Entities.ConversationStep.DraftCustomerName, ct, OrderDraft.Empty());
                break;
            case 2:
                await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskTrackRef,
                    Domain.Entities.ConversationStep.AwaitingTrackRef, ct);
                break;
            case 3:
                await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.Help,
                    Domain.Entities.ConversationStep.Idle, ct);
                break;
            default:
                await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.Menu,
                    Domain.Entities.ConversationStep.AwaitingMenuChoice, ct);
                break;
        }
    }

    private async Task HandleFreeTextOrderAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string text, CancellationToken ct)
    {
        ParsedOrder parsed;
        try
        {
            parsed = await _parser.ParseOrderTextAsync(text, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Order parse failed, starting guided flow");
            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskCustomerName,
                Domain.Entities.ConversationStep.DraftCustomerName, ct, OrderDraft.Empty());
            return;
        }

        var draft = new OrderDraft(
            _sanitizer.Clean(parsed.CustomerName, 120),
            _sanitizer.Clean(PhoneNormalizer.Normalize(parsed.CustomerPhone), 20),
            _sanitizer.Clean(parsed.Address, 500),
            parsed.Items.Select(i => new DraftItem(
                _sanitizer.Clean(i.Description, 300),
                i.Quantity <= 0 ? 1 : i.Quantity,
                i.UnitPriceNgn)).ToList(),
            parsed.TotalNgn);

        if (draft.IsComplete())
        {
            await ReplyAndSaveAsync(state, phone, replyTo, ConfirmText(draft),
                Domain.Entities.ConversationStep.Confirming, ct, draft);
            return;
        }

        var next = FirstMissingStep(draft);
        await ReplyAndSaveAsync(state, phone, replyTo,
            ConversationTexts.MissingDetails(draft.MissingFields()) + "\n\n" + PromptFor(next),
            next, ct, draft);
    }

    private async Task HandleItemsStepAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string text, OrderDraft draft, CancellationToken ct)
    {
        ParsedOrder parsed;
        try
        {
            parsed = await _parser.ParseOrderTextAsync(text, ct);
        }
        catch
        {
            parsed = new ParsedOrder("", "", "", new List<ParsedItem>(), 0);
        }

        if (parsed.Items.Count == 0)
        {
            await ReplyAndSaveAsync(state, phone, replyTo,
                "I couldn't pick out any items. Try e.g:\n2x Sneakers @22500",
                state.Step, ct);
            return;
        }

        draft = draft with
        {
            Items = parsed.Items.Select(i => new DraftItem(
                _sanitizer.Clean(i.Description, 300),
                i.Quantity <= 0 ? 1 : i.Quantity,
                i.UnitPriceNgn)).ToList()
        };
        if (parsed.TotalNgn > 0) draft = draft with { TotalNgn = parsed.TotalNgn };

        if (draft.TotalNgn > 0)
            await ReplyAndSaveAsync(state, phone, replyTo, ConfirmText(draft),
                Domain.Entities.ConversationStep.Confirming, ct, draft);
        else
            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskAmount,
                Domain.Entities.ConversationStep.DraftAmount, ct, draft);
    }

    private async Task HandleAmountStepAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string text, OrderDraft draft, CancellationToken ct)
    {
        var digits = new string(text.Where(char.IsDigit).ToArray());
        if (long.TryParse(digits, out var amount) && amount > 0)
        {
            draft = draft with { TotalNgn = amount };
            await ReplyAndSaveAsync(state, phone, replyTo, ConfirmText(draft),
                Domain.Entities.ConversationStep.Confirming, ct, draft);
        }
        else
        {
            await ReplyAndSaveAsync(state, phone, replyTo,
                "I need a number for the total, e.g. 50000.",
                state.Step, ct);
        }
    }

    private async Task HandleConfirmStepAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string text, OrderDraft draft, CancellationToken ct)
    {
        var upper = text.ToUpperInvariant();
        if (upper is "YES" or "Y" or "CONFIRM")
        {
            await CreateOrderFromDraftAsync(state, phone, replyTo, draft, ct);
            return;
        }

        await ReplyAndSaveAsync(state, phone, replyTo, ConfirmText(draft) + "\n(Reply YES or CANCEL)",
            state.Step, ct);
    }

    private async Task CreateOrderFromDraftAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, OrderDraft draft, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new CreateOrderCommand(
                phone,
                draft.CustomerName,
                draft.CustomerPhone,
                draft.Address,
                draft.Items.Select(i => new OrderItemInput(i.Description, i.Quantity, i.UnitPriceNgn)).ToList(),
                draft.TotalNgn,
                $"{phone}@whatsapp.instasafe"), ct);

            if (result.IsSuccess)
            {
                Reset(state);
                state.Touch();
                await _states.SaveAsync(ct);
                await SendWithTimeoutAsync(replyTo,
                    ConversationTexts.OrderCreated(result.Value!.AmountKobo, result.Value.PaystackAuthUrl));
                return;
            }

            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.OrderFailed(result.Error ?? "unknown error"),
                Domain.Entities.ConversationStep.Idle, ct);
            Reset(state);
            state.Touch();
            await _states.SaveAsync(ct);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Draft failed validation, asking for missing fields");
            var next = FirstMissingStep(draft);
            await ReplyAndSaveAsync(state, phone, replyTo,
                ConversationTexts.MissingDetails(draft.MissingFields()) + "\n\n" + PromptFor(next),
                next, ct, draft);
        }
        catch (Domain.Exceptions.DomainValidationException ex)
        {
            _logger.LogWarning(ex, "Draft failed domain validation");
            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.OrderFailed(ex.Message),
                state.Step, ct, draft);
        }
    }

    private async Task LookupAndReplyTrackAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string reference, CancellationToken ct)
    {
        var refTrimmed = reference.Trim();
        Domain.Entities.Order? order = null;
        if (Guid.TryParse(refTrimmed, out var id))
            order = await _orders.GetByIdAsync(id, ct);
        order ??= await _orders.GetByPaystackRefAsync(refTrimmed, ct);

        if (order is null)
        {
            state.Step = Domain.Entities.ConversationStep.Idle;
            state.Touch();
            await _states.SaveAsync(ct);
            await SendWithTimeoutAsync(replyTo, ConversationTexts.TrackNotFound(refTrimmed));
            return;
        }

        var displayRef = order.PaystackReference ?? order.Id.ToString();
        Reset(state);
        state.Touch();
        await _states.SaveAsync(ct);
        await SendWithTimeoutAsync(replyTo,
            ConversationTexts.TrackResult(displayRef, order.Status.ToString(), order.AmountKobo));
    }

    private async Task EnsureVendorAsync(string phone, CancellationToken ct)
    {
        var vendor = await _vendors.GetByPhoneAsync(phone, ct);
        if (vendor is null)
            await _vendors.AddAsync(new Domain.Entities.Vendor
            {
                Phone = phone,
                DisplayName = phone,
                IsActive = true
            }, ct);
    }

    private static void Reset(Domain.Entities.ConversationState state)
    {
        state.Step = Domain.Entities.ConversationStep.Idle;
        state.DraftJson = null;
    }

    private async Task ReplyAndSaveAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string reply,
        Domain.Entities.ConversationStep next, CancellationToken ct, OrderDraft? draft = null)
    {
        state.Step = next;
        if (draft is not null) state.DraftJson = draft.Save();
        else if (next == Domain.Entities.ConversationStep.Idle
            || next == Domain.Entities.ConversationStep.AwaitingMenuChoice) state.DraftJson = null;
        state.Touch();
        await _states.SaveAsync(ct);
        await SendWithTimeoutAsync(replyTo, reply);
    }

    private async Task SendWithTimeoutAsync(string replyTo, string reply)
    {
        // Replies must not die with the inbound HTTP request: OpenWA/tunnel may
        // close the delivery connection while Azure is still sending. 30s cap
        // keeps a hung gateway from holding the thread forever.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _sender.SendTextAsync(replyTo, reply, cts.Token);
    }

    private static string ConfirmText(OrderDraft draft) =>
        ConversationTexts.ConfirmSummary(
            draft.CustomerName, draft.CustomerPhone, draft.Address,
            draft.ItemsSummary(), draft.TotalNgn);

    private static Domain.Entities.ConversationStep FirstMissingStep(OrderDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.CustomerName)) return Domain.Entities.ConversationStep.DraftCustomerName;
        if (string.IsNullOrWhiteSpace(draft.CustomerPhone)) return Domain.Entities.ConversationStep.DraftCustomerPhone;
        if (string.IsNullOrWhiteSpace(draft.Address)) return Domain.Entities.ConversationStep.DraftAddress;
        if (draft.Items.Count == 0) return Domain.Entities.ConversationStep.DraftItems;
        return Domain.Entities.ConversationStep.DraftAmount;
    }

    private static string PromptFor(Domain.Entities.ConversationStep step) => step switch
    {
        Domain.Entities.ConversationStep.DraftCustomerName => ConversationTexts.AskCustomerName,
        Domain.Entities.ConversationStep.DraftCustomerPhone => ConversationTexts.AskCustomerPhone,
        Domain.Entities.ConversationStep.DraftAddress => ConversationTexts.AskAddress,
        Domain.Entities.ConversationStep.DraftItems => ConversationTexts.AskItems,
        Domain.Entities.ConversationStep.DraftAmount => ConversationTexts.AskAmount,
        _ => ConversationTexts.Menu
    };
}
