using InstaSafe.Domain.Common;
using InstaSafe.Domain.Enums;

namespace InstaSafe.Domain.Entities;

public class WebhookEventLog : BaseEntity
{
    public required string Source { get; set; }
    public string? EventType { get; set; }
    public required string RawPayload { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public WebhookProcessingResult? ProcessingResult { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AlatPayTransactionId { get; set; }
    public bool IsProcessed { get; set; } = false;
}
