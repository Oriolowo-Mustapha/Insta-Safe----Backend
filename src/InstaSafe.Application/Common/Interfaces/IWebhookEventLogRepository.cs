using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Common.Interfaces;

public interface IWebhookEventLogRepository
{
    Task<bool> IsProcessedAsync(string transactionReference, CancellationToken ct);
    void Add(WebhookEventLog log);
}
