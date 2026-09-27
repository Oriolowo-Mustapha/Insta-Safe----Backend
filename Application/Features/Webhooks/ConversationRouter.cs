using FluentValidation;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Features.Orders.Commands.CreateOrder;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Features.Webhooks;

public class ConversationRouter
{
    public static readonly TimeSpan StateExpiry = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan DraftRetention = TimeSpan.FromDays(7);

    private readonly IConversationRepository _states;
    private readonly IVendorRepository _vendors;
    private readonly IOrderRepository _orders;
    private readonly IGroqParser _parser;
    private readonly IWhatsAppSender _sender;
    private readonly IMediator _mediator;
    private readonly ISanitizer _sanitizer;
    private readonly IConfiguration _config;
    private readonly BankDirectory _banks;
    private readonly IPaystackClient _paystack;
    private readonly ILogger<ConversationRouter> _logger;

    public ConversationRouter(
        IConversationRepository states, IVendorRepository vendors, IOrderRepository orders,
        IGroqParser parser, IWhatsAppSender sender, IMediator mediator,
        ISanitizer sanitizer, IConfiguration config, BankDirectory banks,
        IPaystackClient paystack, ILogger<ConversationRouter> logger)
    {
        _states = states; _vendors = vendors; _orders = orders;
        _parser = parser; _sender = sender; _mediator = mediator;
        _sanitizer = sanitizer; _config = config; _banks = banks;
        _paystack = paystack; _logger = logger;
    }

    public async Task<bool> RouteAsync(string rawPhone, string body, string? replyJid, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(rawPhone);
        var replyTo = string.IsNullOrWhiteSpace(replyJid) ? phone : replyJid.Trim();
        var text = (body ?? "").Trim();
        if (string.IsNullOrEmpty(text)) return false;

        // Bot access gate: signed-up + email-verified + onboarded vendors only.
        // No state is created for strangers; one short nudge and stop.
        var gate = await CheckAccessAsync(phone, ct);
        if (gate is not null)
        {
            await SendWithTimeoutAsync(replyTo, gate);
            return true;
        }

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
            await PersistAndResetAsync(state, ct);
            await ReplyAndSaveAsync(state, phone, replyTo,
                ConversationTexts.SessionExpired + "\n" + ConversationTexts.ProgressSaved +
                "\n\n" + await GetMenuAsync(phone, ct),
                Domain.Entities.ConversationStep.AwaitingMenuChoice, ct);
            return true;
        }
        var upper = text.ToUpperInvariant();
        if (upper is "MENU")
        {
            await PersistAndResetAsync(state, ct);
            await ReplyAndSaveAsync(state, phone, replyTo, await GetMenuAsync(phone, ct),
                Domain.Entities.ConversationStep.AwaitingMenuChoice, ct);
            return true;
        }
        if (upper is "CANCEL" or "STOP")
        {
            await PersistAndResetAsync(state, ct);
            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.Cancelled,
                Domain.Entities.ConversationStep.Idle, ct);
            return true;
        }
        if (upper is "BACK" or "EDIT")
        {
            await HandleBackStepAsync(state, phone, replyTo, ct);
            return true;
        }

        var draft = OrderDraft.Load(state.DraftJson);

        switch (state.Step)
        {
            case Domain.Entities.ConversationStep.Idle:
            case Domain.Entities.ConversationStep.AwaitingMenuChoice:                await HandleMenuOrIntentAsync(state, phone, replyTo, text, draft, ct);
                break;
            case Domain.Entities.ConversationStep.BrowsingDrafts:
                await HandleBrowseStepAsync(state, phone, replyTo, text, ct);
                break;
            case Domain.Entities.ConversationStep.DraftCustomerName:
                if (text.Length > 120) { await ReplyAndSaveAsync(state, phone, replyTo, "That name is too long — please send a shorter name.", state.Step, ct); break; }
                draft = draft with { CustomerName = _sanitizer.Clean(text, 120) };
                await AdvanceAsync(state, phone, replyTo, draft, ct);
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
                await AdvanceAsync(state, phone, replyTo, draft, ct);
                break;
            case Domain.Entities.ConversationStep.DraftBuyerEmail:
                await HandleBuyerEmailStepAsync(state, phone, replyTo, text, draft, ct);
                break;
            case Domain.Entities.ConversationStep.DraftAddress:
                draft = draft with { Address = _sanitizer.Clean(text, 500) };
                await AdvanceAsync(state, phone, replyTo, draft, ct);
                break;
            case Domain.Entities.ConversationStep.DraftItems:
                await HandleItemsStepAsync(state, phone, replyTo, text, draft, ct);
                break;
            case Domain.Entities.ConversationStep.DraftAmount:
                await HandleAmountStepAsync(state, phone, replyTo, text, draft, ct);
                break;
            case Domain.Entities.ConversationStep.DraftDeliveryFee:
                await HandleFeeStepAsync(state, phone, replyTo, text, draft, ct);
                break;
            case Domain.Entities.ConversationStep.DraftDriverPhone:
                await HandleDriverPhoneStepAsync(state, phone, replyTo, text, draft, ct);
                break;
            case Domain.Entities.ConversationStep.DraftDriverAccount:
                await HandleDriverAccountStepAsync(state, phone, replyTo, text, draft, ct);
                break;
            case Domain.Entities.ConversationStep.DraftDriverBankName:
                await HandleDriverBankNameStepAsync(state, phone, replyTo, text, draft, ct);
                break;
            case Domain.Entities.ConversationStep.DraftDriverConfirm:
                await HandleDriverConfirmStepAsync(state, phone, replyTo, text, draft, ct);
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
        if (text is "4")
        {
            await ShowDraftListAsync(state, phone, replyTo, ct);
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
                    ConversationTexts.Welcome + "\n\n" + await GetMenuAsync(phone, ct),
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
                    "I didn't quite get that. " + await GetMenuAsync(phone, ct),
                    Domain.Entities.ConversationStep.AwaitingMenuChoice, ct);
                break;
        }
    }

    private async Task ShowDraftListAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, CancellationToken ct)
    {
        var drafts = await GetOpenDraftsAsync(phone, ct);
        if (drafts.Count == 0)
        {
            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.NoDrafts,
                Domain.Entities.ConversationStep.AwaitingMenuChoice, ct);
            return;
        }
        var summaries = drafts
            .Select(d => OrderDraft.Load(d.DraftJson).OneLineSummary())
            .ToList();
        await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.DraftList(summaries),
            Domain.Entities.ConversationStep.BrowsingDrafts, ct);
    }

    private async Task HandleBrowseStepAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string text, CancellationToken ct)
    {
        var upper = text.ToUpperInvariant();
        if (upper.StartsWith("D") && int.TryParse(upper[1..], out var discard) && discard >= 1)
        {
            var drafts = await GetOpenDraftsAsync(phone, ct);
            if (discard <= drafts.Count)
            {
                var tracked = await _states.GetDraftAsync(drafts[discard - 1].Id, ct);
                if (tracked is not null)
                {
                    tracked.Status = Domain.Entities.DraftTicketStatus.Discarded;
                    await _states.SaveAsync(ct);
                }
                await ShowDraftListAsync(state, phone, replyTo, ct);
                return;
            }
        }

        if (int.TryParse(text.Trim(), out var pick) && pick >= 1)
        {
            var drafts = await GetOpenDraftsAsync(phone, ct);
            if (pick <= drafts.Count)
            {
                var chosen = drafts[pick - 1];
                var draft = OrderDraft.Load(chosen.DraftJson);
                state.CurrentDraftId = chosen.Id;
                var next = draft.IsComplete()
                    ? Domain.Entities.ConversationStep.Confirming
                    : FirstMissingStep(draft);
                var intro = ConversationTexts.DraftLoaded + "\n\n" +
                    (next == Domain.Entities.ConversationStep.Confirming
                        ? ConfirmText(draft)
                        : PromptFor(next));
                await ReplyAndSaveAsync(state, phone, replyTo, intro, next, ct, draft);
                return;
            }
        }

        await ShowDraftListAsync(state, phone, replyTo, ct);
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
            case 4:
                await ShowDraftListAsync(state, phone, replyTo, ct);
                break;
            default:
                await ReplyAndSaveAsync(state, phone, replyTo, await GetMenuAsync(phone, ct),
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
            parsed.TotalNgn,
            parsed.DeliveryFeeNgn < 0 ? 0 : parsed.DeliveryFeeNgn,
            _sanitizer.Clean(PhoneNormalizer.Normalize(parsed.DriverPhone), 20),
            BuyerEmail: InstaSafe.Application.Common.Helpers.EmailChecker.IsPlausible(parsed.BuyerEmail)
                ? _sanitizer.Clean(parsed.BuyerEmail.Trim().ToLowerInvariant(), 200)
                : "");

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
            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskDeliveryFee,
                Domain.Entities.ConversationStep.DraftDeliveryFee, ct, draft);
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
            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskDeliveryFee,
                Domain.Entities.ConversationStep.DraftDeliveryFee, ct, draft);
        }
        else
        {
            await ReplyAndSaveAsync(state, phone, replyTo,
                "I need a number for the total, e.g. 50000.",
                state.Step, ct);
        }
    }

    private async Task HandleFeeStepAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string text, OrderDraft draft, CancellationToken ct)
    {
        var digits = new string(text.Where(char.IsDigit).ToArray());
        if (!long.TryParse(string.IsNullOrEmpty(digits) ? "0" : digits, out var fee) || fee < 0)
        {
            await ReplyAndSaveAsync(state, phone, replyTo,
                "Send the delivery fee as a number, e.g. 5000 — or 0 for none.",
                state.Step, ct);
            return;
        }
        draft = draft with { DeliveryFeeNgn = fee };
        if (fee == 0)
        {
            await AdvanceAsync(state, phone, replyTo, draft, ct);
            return;
        }
        await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskDriverPhone,
            Domain.Entities.ConversationStep.DraftDriverPhone, ct, draft);
    }

    private async Task HandleDriverPhoneStepAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string text, OrderDraft draft, CancellationToken ct)
    {
        var upper = text.ToUpperInvariant();
        if (upper is "SKIP" or "NONE" or "0")
        {
            draft = draft with { DeliveryFeeNgn = 0, DriverPhone = "", DriverAccountNumber = "", DriverBankCode = "", DriverBankName = "", DriverHolderName = "" };
            await AdvanceAsync(state, phone, replyTo, draft, ct);
            return;
        }
        var driverPhone = PhoneNormalizer.Normalize(text);
        if (driverPhone.Length < 7)
        {
            await ReplyAndSaveAsync(state, phone, replyTo,
                "That doesn't look like a phone number. Send the driver's number or SKIP.",
                state.Step, ct);
            return;
        }
        draft = draft with { DriverPhone = _sanitizer.Clean(driverPhone, 20) };
        await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskDriverAccount,
            Domain.Entities.ConversationStep.DraftDriverAccount, ct, draft);
    }

    private async Task HandleDriverAccountStepAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string text, OrderDraft draft, CancellationToken ct)
    {
        var digits = new string(text.Where(char.IsDigit).ToArray());
        if (digits.Length < 10)
        {
            await ReplyAndSaveAsync(state, phone, replyTo,
                "Account number should be at least 10 digits. Please resend it.",
                state.Step, ct);
            return;
        }
        draft = draft with { DriverAccountNumber = _sanitizer.Clean(digits, 20) };
        await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.AskDriverBankName,
            Domain.Entities.ConversationStep.DraftDriverBankName, ct, draft);
    }

    private async Task HandleBuyerEmailStepAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string text, OrderDraft draft, CancellationToken ct)
    {
        var email = text.Trim().ToLowerInvariant();
        if (!EmailChecker.IsPlausible(email))
        {
            await ReplyAndSaveAsync(state, phone, replyTo,
                "That doesn't look like an email address. Please send the buyer's email (e.g. chidi@example.com).",
                state.Step, ct);
            return;
        }
        draft = draft with { BuyerEmail = _sanitizer.Clean(email, 200) };
        await AdvanceAsync(state, phone, replyTo, draft, ct);
    }

    private async Task HandleDriverBankNameStepAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string text, OrderDraft draft, CancellationToken ct)
    {
        BankInfo? bank;
        try
        {
            var banks = await _banks.GetBanksAsync(ct);
            bank = BankDirectory.Match(text, banks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bank match failed");
            bank = null;
        }

        if (bank is null)
        {
            await ReplyAndSaveAsync(state, phone, replyTo,
                "I couldn't find that bank. Send the full bank name (e.g. Guaranty Trust Bank) or its 3-digit code.",
                state.Step, ct);
            return;
        }

        InstaSafe.Application.Common.Interfaces.AccountResolveResult resolved;
        try
        {
            resolved = await _paystack.ResolveAccountAsync(draft.DriverAccountNumber, bank.Code, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver account resolve failed");
            resolved = new AccountResolveResult(
                false, null, ResolveFailureKind.Unavailable,
                "Bank verification is temporarily unavailable.");
        }

        if (!resolved.Success)
        {
            if (resolved.FailureKind == ResolveFailureKind.Unavailable)
            {
                // Verification service down (e.g. test-mode daily limit):
                // don't block the order — vendor self-checks and continues.
                draft = draft with
                {
                    DriverBankCode = bank.Code,
                    DriverBankName = bank.Name,
                    DriverHolderName = ""
                };
                await ReplyAndSaveAsync(state, phone, replyTo,
                    $"Bank verification is temporarily unavailable, so I couldn't confirm the holder name.\n" +
                    $"Please double-check these yourself — {bank.Name}, {draft.DriverAccountNumber}. " +
                    "If they're correct, we'll continue.",
                    Domain.Entities.ConversationStep.Confirming, ct, draft);
                await ReplyAndSaveAsync(state, phone, replyTo, ConfirmText(draft),
                    Domain.Entities.ConversationStep.Confirming, ct, draft);
                return;
            }
            await ReplyAndSaveAsync(state, phone, replyTo,
                $"I couldn't verify account {draft.DriverAccountNumber} at {bank.Name}. " +
                "Please resend the driver account number, or CANCEL to stop.",
                Domain.Entities.ConversationStep.DraftDriverAccount, ct,
                draft with { DriverBankCode = "", DriverBankName = "" });
            return;
        }

        var holder = resolved.AccountName!;
        draft = draft with { DriverBankCode = bank.Code, DriverBankName = bank.Name, DriverHolderName = holder };
        await ReplyAndSaveAsync(state, phone, replyTo,
            ConversationTexts.DriverDetailsConfirm(bank.Name, draft.DriverAccountNumber, holder),
            Domain.Entities.ConversationStep.DraftDriverConfirm, ct, draft);
    }

    private async Task HandleDriverConfirmStepAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string text, OrderDraft draft, CancellationToken ct)
    {
        var upper = text.ToUpperInvariant();
        if (upper is "YES" or "Y" or "CONFIRM")
        {
            await ReplyAndSaveAsync(state, phone, replyTo, ConfirmText(draft),
                Domain.Entities.ConversationStep.Confirming, ct, draft);
            return;
        }

        await ReplyAndSaveAsync(state, phone, replyTo,
            ConversationTexts.DriverDetailsConfirm(
                draft.DriverBankName, draft.DriverAccountNumber, draft.DriverHolderName) +
                "\n(Reply YES to use these details, or CANCEL to stop)",
            state.Step, ct);
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
                string.IsNullOrWhiteSpace(draft.BuyerEmail)
                    ? $"{phone}@whatsapp.instasafe"
                    : draft.BuyerEmail,
                Fulfillment: Domain.Enums.FulfillmentType.Dispatch,
                DeliveryFeeNgn: draft.DeliveryFeeNgn,
                DriverPhone: string.IsNullOrWhiteSpace(draft.DriverPhone) ? null : draft.DriverPhone,
                DriverAccountNumber: string.IsNullOrWhiteSpace(draft.DriverAccountNumber) ? null : draft.DriverAccountNumber,
                DriverBankCode: string.IsNullOrWhiteSpace(draft.DriverBankCode) ? null : draft.DriverBankCode), ct);

            if (result.IsSuccess)
            {
                if (state.CurrentDraftId is not null)
                {
                    var ticket = await _states.GetDraftAsync(state.CurrentDraftId.Value, ct);
                    if (ticket is not null)
                    {
                        ticket.Status = Domain.Entities.DraftTicketStatus.Completed;
                        ticket.CompletedOrderId = result.Value!.Id;
                    }
                }
                Reset(state);
                state.Touch();
                await _states.SaveAsync(ct);
                // Payment link already went to the CUSTOMER (handler notifies
                // WhatsApp + email); the vendor just gets confirmation.
                await SendWithTimeoutAsync(replyTo,
                    ConversationTexts.VendorOrderSent(
                        result.Value!.CustomerName, result.Value.AmountKobo));
                return;
            }

            await PersistAndResetAsync(state, ct);
            await ReplyAndSaveAsync(state, phone, replyTo, ConversationTexts.OrderFailed(result.Error ?? "unknown error"),
                Domain.Entities.ConversationStep.Idle, ct);
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
        catch (Exception ex)
        {
            // Anything else (Paystack/network/DB): never leave the vendor hanging.
            // Draft stays resumable via Continue; details stay in logs + App Insights.
            _logger.LogError(ex, "Order creation failed for {Phone}", phone);
            await PersistAndResetAsync(state, ct);
            await ReplyAndSaveAsync(state, phone, replyTo,
                "Something went wrong creating your order. Your progress is saved — try again from 4. Continue, or type MENU.",
                Domain.Entities.ConversationStep.Idle, ct);
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

    /// <summary>
    /// Bot access gate. Returns null when the sender may use the bot,
    /// otherwise the one-shot nudge to send instead (no state created).
    /// </summary>
    private async Task<string?> CheckAccessAsync(string phone, CancellationToken ct)
    {
        var vendor = await _vendors.GetByPhoneAsync(phone, ct);
        if (vendor is null || !vendor.EmailVerified)
        {
            var baseUrl = (_config["Frontend:BaseUrl"] ?? "").TrimEnd('/');
            return ConversationTexts.SignupRequired(baseUrl);
        }
        if (!vendor.IsActive)
            return ConversationTexts.AccountDeactivated;
        if (!vendor.OnboardingCompleted)
        {
            var baseUrl = (_config["Frontend:BaseUrl"] ?? "").TrimEnd('/');
            return ConversationTexts.OnboardingRequired(baseUrl);
        }
        return null;
    }

    private async Task HandleBackStepAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, CancellationToken ct)
    {
        var draft = OrderDraft.Load(state.DraftJson);
        var prev = PreviousStep(state.Step, draft);
        if (prev is null)
        {
            await ReplyAndSaveAsync(state, phone, replyTo,
                "Nothing to go back to — " + PromptFor(state.Step), state.Step, ct);
            return;
        }
        var label = StepLabel(prev.Value);
        await ReplyAndSaveAsync(state, phone, replyTo,
            $"Going back — {label}\n\n" + PromptFor(prev.Value), prev.Value, ct, draft);
    }

    /// <summary>
    /// Previous ANSWERED step in canonical order, so re-answering flows
    /// forward again via AdvanceAsync. Null when already at the start.
    /// </summary>
    private static Domain.Entities.ConversationStep? PreviousStep(
        Domain.Entities.ConversationStep current, OrderDraft draft)
    {
        var chain = new List<(Domain.Entities.ConversationStep Step, bool Answered)>
        {
            (Domain.Entities.ConversationStep.DraftCustomerName, !string.IsNullOrWhiteSpace(draft.CustomerName)),
            (Domain.Entities.ConversationStep.DraftCustomerPhone, !string.IsNullOrWhiteSpace(draft.CustomerPhone)),
            (Domain.Entities.ConversationStep.DraftBuyerEmail, !string.IsNullOrWhiteSpace(draft.BuyerEmail)),
            (Domain.Entities.ConversationStep.DraftAddress, !string.IsNullOrWhiteSpace(draft.Address)),
            (Domain.Entities.ConversationStep.DraftItems, draft.Items.Count > 0),
            (Domain.Entities.ConversationStep.DraftAmount, draft.TotalNgn > 0),
            (Domain.Entities.ConversationStep.DraftDeliveryFee, true),
            (Domain.Entities.ConversationStep.DraftDriverPhone, !string.IsNullOrWhiteSpace(draft.DriverPhone)),
            (Domain.Entities.ConversationStep.DraftDriverAccount, !string.IsNullOrWhiteSpace(draft.DriverAccountNumber)),
            (Domain.Entities.ConversationStep.DraftDriverBankName, !string.IsNullOrWhiteSpace(draft.DriverBankCode)),
            (Domain.Entities.ConversationStep.DraftDriverConfirm, !string.IsNullOrWhiteSpace(draft.DriverBankCode)),
            (Domain.Entities.ConversationStep.Confirming, false)
        };
        var idx = chain.FindIndex(x => x.Step == current);
        if (idx < 0) return null;
        for (var i = idx - 1; i >= 0; i--)
            if (chain[i].Answered) return chain[i].Step;
        return null;
    }

    private static string StepLabel(Domain.Entities.ConversationStep step) => step switch
    {
        Domain.Entities.ConversationStep.DraftCustomerName => "customer name",
        Domain.Entities.ConversationStep.DraftCustomerPhone => "customer phone",
        Domain.Entities.ConversationStep.DraftBuyerEmail => "buyer email",
        Domain.Entities.ConversationStep.DraftAddress => "delivery address",
        Domain.Entities.ConversationStep.DraftItems => "items",
        Domain.Entities.ConversationStep.DraftAmount => "total amount",
        Domain.Entities.ConversationStep.DraftDeliveryFee => "delivery fee",
        Domain.Entities.ConversationStep.DraftDriverPhone => "driver phone",
        Domain.Entities.ConversationStep.DraftDriverAccount => "driver account",
        Domain.Entities.ConversationStep.DraftDriverBankName => "driver bank",
        Domain.Entities.ConversationStep.DraftDriverConfirm => "driver details",
        _ => "previous question"
    };

    private static void Reset(Domain.Entities.ConversationState state)
    {
        state.Step = Domain.Entities.ConversationStep.Idle;
        state.DraftJson = null;
        state.CurrentDraftId = null;
    }

    /// <summary>
    /// Saves in-progress work into its draft ticket (creating one when the
    /// draft has content but no ticket yet), drops empty tickets, then resets
    /// the live state. Drafts are never silently discarded.
    /// </summary>
    private async Task PersistAndResetAsync(Domain.Entities.ConversationState state, CancellationToken ct)
    {
        var draft = OrderDraft.Load(state.DraftJson);
        if (state.CurrentDraftId is not null)
        {
            var ticket = await _states.GetDraftAsync(state.CurrentDraftId.Value, ct);
            if (ticket is not null)
            {
                if (draft.IsEmpty())
                    await _states.RemoveDraftAsync(ticket, ct);
                else if (ticket.Status == Domain.Entities.DraftTicketStatus.Open)
                    ticket.DraftJson = draft.Save();
            }
        }
        else if (!draft.IsEmpty())
        {
            await _states.AddDraftAsync(new Domain.Entities.SavedOrderDraft
            {
                VendorPhone = state.Phone,
                DraftJson = draft.Save(),
                Status = Domain.Entities.DraftTicketStatus.Open
            }, ct);
        }
        Reset(state);
    }

    /// <summary>Open drafts, lazily abandoning ones untouched for 7 days.</summary>
    private async Task<List<Domain.Entities.SavedOrderDraft>> GetOpenDraftsAsync(
        string phone, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow - DraftRetention;
        var drafts = await _states.ListDraftsAsync(phone, Domain.Entities.DraftTicketStatus.Open, ct);
        var fresh = new List<Domain.Entities.SavedOrderDraft>();
        foreach (var d in drafts)
        {
            var touched = d.UpdatedAt ?? d.CreatedAt;
            if (touched < cutoff)
            {
                var tracked = await _states.GetDraftAsync(d.Id, ct);
                if (tracked is not null) tracked.Status = Domain.Entities.DraftTicketStatus.Abandoned;
            }
            else
            {
                fresh.Add(d);
            }
        }
        return fresh;
    }

    private async Task<string> GetMenuAsync(string phone, CancellationToken ct)
    {
        var open = await GetOpenDraftsAsync(phone, ct);
        return open.Count == 0
            ? ConversationTexts.Menu
            : ConversationTexts.MenuWithContinue(open.Count);
    }

    private async Task ReplyAndSaveAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, string reply,
        Domain.Entities.ConversationStep next, CancellationToken ct, OrderDraft? draft = null)
    {
        state.Step = next;
        if (draft is not null)
        {
            state.DraftJson = draft.Save();
            await EnsureTicketAsync(state, draft, ct);
        }
        else if (next == Domain.Entities.ConversationStep.Idle
            || next == Domain.Entities.ConversationStep.AwaitingMenuChoice) state.DraftJson = null;
        state.Touch();
        await _states.SaveAsync(ct);
        await SendWithTimeoutAsync(replyTo, reply);
    }

    /// <summary>
    /// Every draft-bearing step mirrors into a ticket row so progress
    /// survives resets and restarts. Empty drafts never create tickets.
    /// </summary>
    private async Task EnsureTicketAsync(
        Domain.Entities.ConversationState state, OrderDraft draft, CancellationToken ct)
    {
        if (draft.IsEmpty()) return;
        if (state.CurrentDraftId is not null)
        {
            var ticket = await _states.GetDraftAsync(state.CurrentDraftId.Value, ct);
            if (ticket is not null)
            {
                if (ticket.Status == Domain.Entities.DraftTicketStatus.Open)
                    ticket.DraftJson = draft.Save();
                else
                    state.CurrentDraftId = null;
            }
            else
            {
                state.CurrentDraftId = null;
            }
        }
        if (state.CurrentDraftId is null)
        {
            var ticket = new Domain.Entities.SavedOrderDraft
            {
                VendorPhone = state.Phone,
                DraftJson = draft.Save(),
                Status = Domain.Entities.DraftTicketStatus.Open
            };
            await _states.AddDraftAsync(ticket, ct);
            state.CurrentDraftId = ticket.Id;
        }
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
            draft.ItemsSummary(), draft.TotalNgn,
            draft.DeliveryFeeNgn, draft.DriverPhone, draft.DriverBankName,
            draft.DriverHolderName, draft.BuyerEmail);

    private static Domain.Entities.ConversationStep FirstMissingStep(OrderDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.CustomerName)) return Domain.Entities.ConversationStep.DraftCustomerName;
        if (string.IsNullOrWhiteSpace(draft.CustomerPhone)) return Domain.Entities.ConversationStep.DraftCustomerPhone;
        if (string.IsNullOrWhiteSpace(draft.BuyerEmail)) return Domain.Entities.ConversationStep.DraftBuyerEmail;
        if (string.IsNullOrWhiteSpace(draft.Address)) return Domain.Entities.ConversationStep.DraftAddress;
        if (draft.Items.Count == 0) return Domain.Entities.ConversationStep.DraftItems;
        if (draft.TotalNgn <= 0) return Domain.Entities.ConversationStep.DraftAmount;
        if (draft.WantsDispatch)
        {
            if (string.IsNullOrWhiteSpace(draft.DriverPhone)) return Domain.Entities.ConversationStep.DraftDriverPhone;
            if (string.IsNullOrWhiteSpace(draft.DriverAccountNumber)) return Domain.Entities.ConversationStep.DraftDriverAccount;
            if (string.IsNullOrWhiteSpace(draft.DriverBankCode)) return Domain.Entities.ConversationStep.DraftDriverBankName;
        }
        if (!draft.IsComplete()) return Domain.Entities.ConversationStep.DraftDeliveryFee;
        return Domain.Entities.ConversationStep.Confirming;
    }

    /// <summary>
    /// Single advancement rule for every draft step: complete → confirm
    /// summary, otherwise prompt for the first missing field. Never re-asks
    /// for fields already filled (e.g. resumed tickets).
    /// </summary>
    private async Task AdvanceAsync(
        Domain.Entities.ConversationState state, string phone, string replyTo, OrderDraft draft, CancellationToken ct)
    {
        if (draft.IsComplete())
        {
            await ReplyAndSaveAsync(state, phone, replyTo, ConfirmText(draft),
                Domain.Entities.ConversationStep.Confirming, ct, draft);
            return;
        }
        var next = FirstMissingStep(draft);
        await ReplyAndSaveAsync(state, phone, replyTo, PromptFor(next), next, ct, draft);
    }

    private static string PromptFor(Domain.Entities.ConversationStep step) => step switch
    {
        Domain.Entities.ConversationStep.DraftCustomerName => ConversationTexts.AskCustomerName,
        Domain.Entities.ConversationStep.DraftCustomerPhone => ConversationTexts.AskCustomerPhone,
        Domain.Entities.ConversationStep.DraftBuyerEmail => ConversationTexts.AskBuyerEmail,
        Domain.Entities.ConversationStep.DraftAddress => ConversationTexts.AskAddress,
        Domain.Entities.ConversationStep.DraftItems => ConversationTexts.AskItems,
        Domain.Entities.ConversationStep.DraftAmount => ConversationTexts.AskAmount,
        Domain.Entities.ConversationStep.DraftDeliveryFee => ConversationTexts.AskDeliveryFee,
        Domain.Entities.ConversationStep.DraftDriverPhone => ConversationTexts.AskDriverPhone,
        Domain.Entities.ConversationStep.DraftDriverAccount => ConversationTexts.AskDriverAccount,
        Domain.Entities.ConversationStep.DraftDriverBankName => ConversationTexts.AskDriverBankName,
        _ => ConversationTexts.Menu
    };
}
