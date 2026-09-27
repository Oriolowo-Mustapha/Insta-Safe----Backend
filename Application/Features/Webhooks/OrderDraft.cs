using InstaSafe.Domain.Enums;
using System.Text.Json;

namespace InstaSafe.Application.Features.Webhooks;

public sealed record DraftItem(string Description, int Quantity, long UnitPriceNgn);

public sealed record OrderDraft(
    string CustomerName,
    string CustomerPhone,
    string Address,
    List<DraftItem> Items,
    long TotalNgn,
    long DeliveryFeeNgn = 0,
    string DriverPhone = "",
    string DriverAccountNumber = "",
    string DriverBankCode = "",
    string DriverBankName = "",
    string DriverHolderName = "",
    string BuyerEmail = "",
    FulfillmentType? Fulfillment = null)
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

    public bool WantsDispatch => DeliveryFeeNgn > 0 || !string.IsNullOrWhiteSpace(DriverPhone);

    public string MissingFields()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(CustomerName)) missing.Add("customer name");
        if (string.IsNullOrWhiteSpace(CustomerPhone)) missing.Add("customer phone");
        if (string.IsNullOrWhiteSpace(BuyerEmail)) missing.Add("buyer email");
        if (string.IsNullOrWhiteSpace(Address)) missing.Add("delivery address");
        if (Items.Count == 0) missing.Add("items");
        if (TotalNgn <= 0) missing.Add("total amount");
        if (Fulfillment is null) missing.Add("delivery type");
        if (WantsDispatch)
        {
            if (string.IsNullOrWhiteSpace(DriverPhone)) missing.Add("driver phone");
            if (string.IsNullOrWhiteSpace(DriverAccountNumber)) missing.Add("driver account number");
            if (string.IsNullOrWhiteSpace(DriverBankCode)) missing.Add("driver bank code");
        }
        return string.Join(", ", missing);
    }

    public bool IsComplete() => string.IsNullOrEmpty(MissingFields());

    public bool IsEmpty() =>
        string.IsNullOrWhiteSpace(CustomerName)
        && string.IsNullOrWhiteSpace(CustomerPhone)
        && string.IsNullOrWhiteSpace(Address)
        && Items.Count == 0
        && TotalNgn <= 0
        && DeliveryFeeNgn <= 0
        && string.IsNullOrWhiteSpace(DriverPhone)
        && string.IsNullOrWhiteSpace(DriverAccountNumber)
        && string.IsNullOrWhiteSpace(DriverBankCode);

    public string ItemsSummary() =>
        string.Join("\n", Items.Select(i => $"- {i.Quantity}x {i.Description} @ ₦{i.UnitPriceNgn:N0}"));

    public string OneLineSummary()
    {
        var who = string.IsNullOrWhiteSpace(CustomerName) ? "Unnamed customer" : CustomerName;
        var what = Items.Count == 0
            ? "no items yet"
            : string.Join(", ", Items.Select(i => $"{i.Quantity}x {i.Description}"));
        var total = TotalNgn > 0 ? $"₦{(TotalNgn + DeliveryFeeNgn):N0}" : "amount TBD";
        var missing = MissingFields();
        return $"{who} — {what}, {total}" +
            (string.IsNullOrEmpty(missing) ? " (ready to confirm)" : $", missing: {missing}");
    }
}
