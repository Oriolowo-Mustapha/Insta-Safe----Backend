namespace InstaSafe.Application.Common.Interfaces;

public interface IGroqParser
{
    Task<ParsedOrder> ParseOrderTextAsync(string rawText, CancellationToken ct);
    Task<ChatIntent> ClassifyIntentAsync(string rawText, CancellationToken ct);
}

public enum ChatIntentKind
{
    Unknown = 0,
    Greeting = 1,
    MenuSelect = 2,
    CreateOrder = 3,
    TrackOrder = 4,
    Help = 5,
    Cancel = 6
}

public sealed record ChatIntent(ChatIntentKind Kind, int? MenuOption, string? TrackReference);

public sealed record ParsedOrder(
    string CustomerName, string CustomerPhone, string Address,
    List<ParsedItem> Items, long TotalNgn,
    long DeliveryFeeNgn = 0, string DriverPhone = "", string BuyerEmail = "");

public sealed record ParsedItem(string Description, int Quantity, long UnitPriceNgn);
