namespace InstaSafe.Infrastructure.Delivery;

public class QrOptions
{
    public const string SectionName = "Qr";

    public string SigningKey { get; init; } = string.Empty;
    public int ExpirationInMinutes { get; init; } = 15;
    public string Issuer { get; init; } = "InstaSafe";
}
