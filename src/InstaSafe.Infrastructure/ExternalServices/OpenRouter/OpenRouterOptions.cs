namespace InstaSafe.Infrastructure.ExternalServices.OpenRouter;

public class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "google/gemini-2.5-flash"; // User requested gemini 2.5 flash or gpt-4o mini
}
