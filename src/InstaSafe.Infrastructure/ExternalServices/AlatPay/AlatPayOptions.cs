namespace InstaSafe.Infrastructure.ExternalServices.AlatPay;

public class AlatPayOptions
{
    public const string SectionName = "AlatPay";
    public string BaseUrl { get; set; } = string.Empty;
    public string BusinessId { get; set; } = string.Empty;
    public string SubscriptionKey { get; set; } = string.Empty;
    public string PrimaryKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}
