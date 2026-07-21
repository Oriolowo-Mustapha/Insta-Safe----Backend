using InstaSafe.Domain.Common;
using InstaSafe.Domain.Enums;

namespace InstaSafe.Domain.Entities;

public class Dispute : BaseEntity
{
    public Guid OrderId { get; init; }
    public Guid RaisedByBuyerId { get; init; }
    public required string Reason { get; init; }
    public string? EvidenceUrls { get; init; }
    public DisputeStatus Status { get; private set; } = DisputeStatus.Open;
    public string? Resolution { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? ResolvedBy { get; private set; }
    
    // AI Augmentation Fields
    public int? AiConfidenceScore { get; private set; }
    public string? AiAnalysisSummary { get; private set; }

    public virtual Order Order { get; private set; } = null!;
    public virtual Buyer Buyer { get; private set; } = null!;

    public void ResolveAsRefund(string resolution, string resolvedBy, DateTime resolvedAt)
    {
        Status = DisputeStatus.ResolvedRefund;
        Resolution = resolution;
        ResolvedBy = resolvedBy;
        ResolvedAt = resolvedAt;
    }

    public void ResolveAsRelease(string resolution, string resolvedBy, DateTime resolvedAt)
    {
        Status = DisputeStatus.ResolvedRelease;
        Resolution = resolution;
        ResolvedBy = resolvedBy;
        ResolvedAt = resolvedAt;
    }

    public void AugmentWithAi(int confidenceScore, string analysisSummary)
    {
        AiConfidenceScore = confidenceScore;
        AiAnalysisSummary = analysisSummary;
    }
}
