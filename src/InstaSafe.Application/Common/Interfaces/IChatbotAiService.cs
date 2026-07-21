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
    Task<DisputeAnalysisResult> AnalyzeDisputeAsync(string itemDescription, string buyerReason, string? evidenceUrl, CancellationToken cancellationToken = default);
}

public class DisputeAnalysisResult
{
    public int ConfidenceScore { get; set; } // 0-100
    public string Summary { get; set; } = string.Empty;
}
