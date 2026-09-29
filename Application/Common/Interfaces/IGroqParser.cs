namespace InstaSafe.Application.Common.Interfaces;

public interface IGroqParser
{
    Task<ParsedOrder> ParseOrderTextAsync(string rawText, CancellationToken ct);
    Task<ChatIntent> ClassifyIntentAsync(string rawText, CancellationToken ct);
    Task<string> ChatReplyAsync(string rawText, CancellationToken ct);

    /// <summary>
    /// Second-chance interpretation for input that failed a draft step's
    /// validation. Decides whether the vendor is correcting something given
    /// earlier (e.g. "the address is actually 14 Allen, Ikeja" while being
    /// asked for the rider's number) rather than answering the current
    /// question. Returns a proposal only; the router still validates the
    /// proposed value with the same per-field rules before applying it, so a
    /// misparse can never bypass scrutiny. Field is one of the closed set
    /// below, or "none".
    /// </summary>
    Task<CorrectionInterpretation> InterpretCorrectionAsync(
        string rawText, string currentQuestion, string filledSummary, CancellationToken ct);
}

public enum ChatIntentKind
{
    Unknown = 0,
    Greeting = 1,
    MenuSelect = 2,
    CreateOrder = 3,
    TrackOrder = 4,
    Help = 5,
    Cancel = 6,
    ListOrders = 7,
    Chitchat = 8
}

public sealed record ChatIntent(ChatIntentKind Kind, int? MenuOption, string? TrackReference);

public sealed record ParsedOrder(
    string CustomerName, string CustomerPhone, string Address,
    List<ParsedItem> Items, long TotalNgn,
    long DeliveryFeeNgn = 0, string DriverPhone = "", string BuyerEmail = "",
    string FulfillmentHint = "");

public sealed record ParsedItem(string Description, int Quantity, long UnitPriceNgn);

/// <summary>
/// The LLM's proposal for a mid-flow correction. Closed field vocabulary:
/// customer_name, customer_phone, buyer_email, address, amount, delivery_fee,
/// driver_phone, driver_account. Anything else (including driver_bank, which
/// needs a live verification call) must come back as IsCorrection=false so it
/// stays on the guided path.
/// </summary>
public sealed record CorrectionInterpretation(bool IsCorrection, string? Field, string? Value);
