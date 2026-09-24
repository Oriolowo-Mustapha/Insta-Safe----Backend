using System.Text.Json;

namespace InstaSafe.Application.Features.Webhooks;

public sealed record DraftItem(string Description, int Quantity, long UnitPriceNgn);

public sealed record OrderDraft(
    string CustomerName,
    string CustomerPhone,
    string Address,
    List<DraftItem> Items,
    long TotalNgn)
{
    public static OrderDraft Empty() => new("", "", "", new List<DraftItem>(), 0);

    public static OrderDraft Load(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty();
        try
        {
            return JsonSerializer.Deserialize<OrderDraft>(json) ?? Empty();
        }
        catch
        {
            return Empty();
        }
    }

    public string Save() => JsonSerializer.Serialize(this);

    public string MissingFields()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(CustomerName)) missing.Add("customer name");
        if (string.IsNullOrWhiteSpace(CustomerPhone)) missing.Add("customer phone");
        if (string.IsNullOrWhiteSpace(Address)) missing.Add("delivery address");
        if (Items.Count == 0) missing.Add("items");
        if (TotalNgn <= 0) missing.Add("total amount");
        return string.Join(", ", missing);
    }

    public bool IsComplete() => string.IsNullOrEmpty(MissingFields());

    public string ItemsSummary() =>
        string.Join("\n", Items.Select(i => $"- {i.Quantity}x {i.Description} @ ₦{i.UnitPriceNgn:N0}"));
}
