using InstaSafe.Domain.Common;

namespace InstaSafe.Domain.Entities;

public class EscrowLedger : BaseEntity
{
    public Guid OrderId { get; set; }
    public long AmountKobo { get; set; }
    public string Currency { get; set; } = "NGN";
    public DateTimeOffset HeldAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? TransferReference { get; set; }
    public bool IsReleased => ReleasedAt.HasValue;
}

public class OutboxMessage : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
}

public class WebhookEvent : BaseEntity
{
    public string Provider { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public bool SignatureValid { get; set; }
    public bool Processed { get; set; }
    public string? IdempotencyKey { get; set; }
}
