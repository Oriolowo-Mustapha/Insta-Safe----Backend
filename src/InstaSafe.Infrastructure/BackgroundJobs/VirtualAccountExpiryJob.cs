using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Infrastructure.BackgroundJobs;

public class VirtualAccountExpiryJob
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<VirtualAccountExpiryJob> _logger;

    public VirtualAccountExpiryJob(IApplicationDbContext context, IDateTimeProvider dateTimeProvider, ILogger<VirtualAccountExpiryJob> logger)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var now = _dateTimeProvider.UtcNow;

        var orders = await _context.Orders
            .Include(o => o.EscrowTransaction)
            .Where(o => o.Status == OrderStatus.PendingPayment
                        && o.EscrowTransaction != null
                        && o.EscrowTransaction.VirtualAccountExpiresAt != null
                        && o.EscrowTransaction.VirtualAccountExpiresAt <= now)
            .ToListAsync();

        int expiredCount = 0;

        foreach (var order in orders)
        {
            try
            {
                order.Expire();
                
                if (order.EscrowTransaction != null)
                {
                    order.EscrowTransaction.Expire();
                }

                _logger.LogInformation("Expired VA for Order {OrderId}. VA was due at {VirtualAccountExpiresAt}", 
                    order.Id, order.EscrowTransaction?.VirtualAccountExpiresAt);

                expiredCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing VA expiry for Order {OrderId}", order.Id);
            }
        }

        if (expiredCount > 0)
        {
            await _context.SaveChangesAsync(CancellationToken.None);
            _logger.LogInformation("Successfully expired {Count} pending orders with expired virtual accounts.", expiredCount);
        }
    }
}
