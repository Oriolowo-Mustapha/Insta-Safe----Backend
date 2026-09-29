using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Notifications;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using InstaSafe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Infrastructure.Outbox;

public class ReleaseDueOrdersWorker : BackgroundService
{
    public static readonly TimeSpan DigitalAutoReleaseAfter = TimeSpan.FromHours(24);

    /// <summary>
    /// Grace period before a self-delivery order auto-releases.
    ///
    /// A self-delivery order has no rider, so nothing can confirm delivery and
    /// the buyer's OTP is voluntary. Without this backstop their escrow is
    /// stranded forever if they never enter the code. Orders WITH a rider are
    /// deliberately excluded - those still require the rider to confirm, and an
    /// unactioned rider is an operations problem, not an auto-release one.
    /// </summary>
    public static readonly TimeSpan SelfDeliveryAutoReleaseAfter = TimeSpan.FromHours(24);

    private readonly IServiceProvider _sp;
    private readonly ILogger<ReleaseDueOrdersWorker> _logger;

    public ReleaseDueOrdersWorker(IServiceProvider sp, ILogger<ReleaseDueOrdersWorker> logger)
    {
        _sp = sp; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var paystack = scope.ServiceProvider.GetRequiredService<IPaystackClient>();
                var notifier = scope.ServiceProvider.GetRequiredService<OrderNotifier>();

                var now = DateTimeOffset.UtcNow;
                // Two separate queries: Union over Order (which owns Items as JSON)
                // crashes EF query translation (NullReference in the expanding visitor).
                var deliveredDue = await db.Orders
                    .Where(o => o.Status == OrderStatus.Delivered && o.ReleaseDueAt != null && o.ReleaseDueAt <= now)
                    .OrderBy(o => o.ReleaseDueAt)
                    .Take(20)
                    .ToListAsync(ct);
                var digitalDue = await db.Orders
                    .Where(o => o.Status == OrderStatus.Held
                        && o.Fulfillment == FulfillmentType.Digital
                        && o.HeldAt != null
                        && o.HeldAt <= now.Subtract(DigitalAutoReleaseAfter))
                    .OrderBy(o => o.HeldAt)
                    .Take(20)
                    .ToListAsync(ct);
                // Self-delivery (and pre-existing driver-less Dispatch rows from
                // before fulfilment was made explicit): the safety net that keeps
                // a buyer's escrow from being stranded when nobody can confirm.
                var selfDeliveryDue = await db.Orders
                    .Where(o => o.Status == OrderStatus.Held
                        && o.HeldAt != null
                        && o.HeldAt <= now.Subtract(SelfDeliveryAutoReleaseAfter)
                        && (o.Fulfillment == FulfillmentType.SelfDelivery
                            || (o.Fulfillment == FulfillmentType.Dispatch && o.DriverPhone == null)))
                    .OrderBy(o => o.HeldAt)
                    .Take(20)
                    .ToListAsync(ct);
                var due = deliveredDue
                    .Concat(digitalDue)
                    .Concat(selfDeliveryDue)
                    .Distinct()
                    .Take(20)
                    .ToList();

                foreach (var order in due)
                {
                    try
                    {
                        await ReleaseAsync(db, paystack, notifier, order, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Auto-release failed for order {OrderId}", order.Id);
                    }
                }
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Release worker poll failed");
            }
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }

    private static async Task ReleaseAsync(
        AppDbContext db, IPaystackClient paystack, OrderNotifier notifier, Order order, CancellationToken ct)
    {
        var remainder = OrderReleaseCalculator.VendorRemainderKobo(order);
        string? transferRef = null;
        if (order.VendorRecipientCode is not null && remainder > 0)
            transferRef = await paystack.InitiateTransferAsync(
                remainder, order.VendorRecipientCode, $"InstaSafe payout {order.Id}", ct);

        order.Status = OrderStatus.Released;
        order.ReleasedAt = DateTimeOffset.UtcNow;
        order.TransferReference = transferRef;
        order.Touch();
        order.AddDomainEvent(new FundsReleasedEvent(order.Id, remainder, transferRef));

        var ledger = await db.Ledgers
            .Where(l => l.OrderId == order.Id && l.ReleasedAt == null)
            .FirstOrDefaultAsync(ct);
        if (ledger is not null)
        {
            ledger.ReleasedAt = DateTimeOffset.UtcNow;
            ledger.TransferReference = transferRef;
        }

        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "FundsReleased",
            Payload = System.Text.Json.JsonSerializer.Serialize(new { order.Id, transferRef, auto = true })
        });

        await notifier.ReleasedAsync(order, order.BuyerEmail, transferRef);
    }
}
