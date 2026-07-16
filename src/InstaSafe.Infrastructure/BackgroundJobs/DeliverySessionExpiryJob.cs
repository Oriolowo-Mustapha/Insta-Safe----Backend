using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Infrastructure.BackgroundJobs;

public class DeliverySessionExpiryJob
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<DeliverySessionExpiryJob> _logger;

    public DeliverySessionExpiryJob(IApplicationDbContext context, IDateTimeProvider dateTimeProvider, ILogger<DeliverySessionExpiryJob> logger)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var now = _dateTimeProvider.UtcNow;

        var sessions = await _context.DeliverySessions
            .Where(s => s.Status == DeliverySessionStatus.PickedUp
                        && s.ExpiresAt != null
                        && s.ExpiresAt <= now)
            .ToListAsync();

        int expiredCount = 0;

        foreach (var session in sessions)
        {
            try
            {
                session.Expire();

                _logger.LogInformation("Expired delivery session {SessionId} for Order {OrderId}", 
                    session.SessionId, session.OrderId);

                expiredCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing session expiry for Session {SessionId}", session.SessionId);
            }
        }

        if (expiredCount > 0)
        {
            await _context.SaveChangesAsync(CancellationToken.None);
            _logger.LogInformation("Successfully expired {Count} stale delivery sessions.", expiredCount);
        }
    }
}
