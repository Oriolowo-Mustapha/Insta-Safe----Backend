namespace InstaSafe.Infrastructure.ExternalServices.WhatsApp;

public class OpenWaOptions
{
    public const string SectionName = "OpenWA";

    public string BaseUrl { get; set; } = "http://localhost:2785/api";
    public string ApiKey { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}
