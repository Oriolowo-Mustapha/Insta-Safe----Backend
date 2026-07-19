namespace InstaSafe.Application.Common.Interfaces;

public class ChatbotIntentResult
{
    public string Intent { get; set; } = string.Empty; // "CreateOrder", "CheckStatus", "Unknown"
    public string? ExtractedData { get; set; } // JSON of extracted entities
    public string? ReplyMessage { get; set; } // Fallback reply if not understood
}

public interface IChatbotAiService
{
    Task<ChatbotIntentResult> ParseIntentAsync(string message, CancellationToken cancellationToken = default);
}
