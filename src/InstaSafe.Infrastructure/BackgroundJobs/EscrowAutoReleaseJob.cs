using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Infrastructure.BackgroundJobs;

public class EscrowAutoReleaseJob
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<EscrowAutoReleaseJob> _logger;

    public EscrowAutoReleaseJob(IApplicationDbContext context, IDateTimeProvider dateTimeProvider, ILogger<EscrowAutoReleaseJob> logger)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var now = _dateTimeProvider.UtcNow;

        var orders = await _context.Orders
            .Include(o => o.Merchant)
            .Include(o => o.EscrowTransaction)
            .Include(o => o.Dispute)
            .Where(o => o.Status == OrderStatus.Delivered
                        && o.ValidationWindowExpiresAt != null
                        && o.ValidationWindowExpiresAt <= now
                        && o.Dispute == null)
            .ToListAsync();

        int releasedCount = 0;

        foreach (var order in orders)
        {
            try
            {
                var totalAmount = order.EscrowTransaction?.Amount ?? order.Price;
                var commissionRate = order.Merchant.CommissionRate;
                var platformCommission = totalAmount * commissionRate;
                var merchantAmount = totalAmount - platformCommission;

                var payoutSplit = new PayoutSplit
                {
                    OrderId = order.Id,
                    TotalAmount = totalAmount,
                    MerchantAmount = merchantAmount,
                    PlatformCommission = platformCommission,
                    CommissionRate = commissionRate
                };

                _context.Set<PayoutSplit>().Add(payoutSplit);

                order.ReleaseFunds(merchantAmount, platformCommission, now);

                if (order.EscrowTransaction != null)
                {
                    order.EscrowTransaction.MarkAsReleased(now);
                }

                _logger.LogInformation("Auto-released escrow for Order {OrderId}. Merchant: {MerchantAmount}, Platform: {PlatformCommission}", 
                    order.Id, merchantAmount, platformCommission);

                releasedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing auto-release for Order {OrderId}", order.Id);
            }
        }

        if (releasedCount > 0)
        {
            await _context.SaveChangesAsync(CancellationToken.None);
            _logger.LogInformation("Successfully auto-released {Count} orders.", releasedCount);
        }
    }
}
