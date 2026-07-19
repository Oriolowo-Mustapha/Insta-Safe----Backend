namespace InstaSafe.Application.Common.Interfaces;

public class DisputeResolutionResult
{
    public string SuggestedResolution { get; set; } = string.Empty; // "RefundBuyer" or "ReleaseToMerchant"
    public int ConfidenceScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public interface IDisputeResolutionAiService
{
    Task<DisputeResolutionResult> AnalyzeDisputeEvidenceAsync(string itemDescription, List<string> evidenceUrls, CancellationToken cancellationToken = default);
}
