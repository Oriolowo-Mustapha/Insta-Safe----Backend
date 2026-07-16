namespace InstaSafe.Application.Disputes.Queries;

public record DisputeDto(
    Guid Id,
    Guid OrderId,
    string OrderReference,
    Guid RaisedByBuyerId,
    string BuyerName,
    string Reason,
    string? EvidenceUrls,
    string Status,
    string? Resolution,
    DateTime? ResolvedAt,
    string? ResolvedBy,
    DateTime CreatedAt
);
