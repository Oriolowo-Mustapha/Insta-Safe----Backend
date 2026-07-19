using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Infrastructure.Persistence.Repositories;

public class WebhookEventLogRepository : IWebhookEventLogRepository
{
    private readonly IApplicationDbContext _context;

    public WebhookEventLogRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsProcessedAsync(string transactionReference, CancellationToken ct)
    {
        return await _context.WebhookEventLogs
            .AnyAsync(w => w.TransactionReference == transactionReference && w.IsProcessed, ct);
    }

    public void Add(WebhookEventLog log)
    {
        _context.WebhookEventLogs.Add(log);
    }
}
