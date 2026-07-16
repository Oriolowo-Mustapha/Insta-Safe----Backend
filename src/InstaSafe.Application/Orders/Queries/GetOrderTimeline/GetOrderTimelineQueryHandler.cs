using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Orders.Queries.GetOrderTimeline;

public class GetOrderTimelineQueryHandler : IRequestHandler<GetOrderTimelineQuery, Result<List<TimelineEntry>>>
{
    private readonly IApplicationDbContext _context;

    public GetOrderTimelineQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<TimelineEntry>>> Handle(GetOrderTimelineQuery request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.EscrowTransaction)
            .Include(o => o.DeliverySession)
            .Include(o => o.Dispute)
            .Include(o => o.PayoutSplit)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
            return Result<List<TimelineEntry>>.Failure("Order not found.");

        var timeline = new List<TimelineEntry>
        {
            new() { Event = "OrderCreated", Timestamp = order.CreatedAt, Detail = $"Order {order.OrderReference} created." }
        };

        if (order.EscrowLinkExpiresAt.HasValue)
            timeline.Add(new TimelineEntry { Event = "EscrowLinkGenerated", Timestamp = order.CreatedAt, Detail = "Escrow payment link generated." });

        if (order.FundedAt.HasValue)
            timeline.Add(new TimelineEntry { Event = "OrderFunded", Timestamp = order.FundedAt.Value, Detail = $"Payment confirmed. Amount: {order.Price} {order.Currency}." });

        if (order.DeliverySession?.PickupTimestamp.HasValue == true)
            timeline.Add(new TimelineEntry { Event = "ItemPickedUp", Timestamp = order.DeliverySession.PickupTimestamp.Value, Detail = "Item picked up by dispatcher." });

        if (order.DeliveredAt.HasValue)
        {
            timeline.Add(new TimelineEntry { Event = "ItemDelivered", Timestamp = order.DeliveredAt.Value, Detail = "Item delivered. 24h validation window started." });

            if (order.ValidationWindowExpiresAt.HasValue)
                timeline.Add(new TimelineEntry { Event = "ValidationWindowExpires", Timestamp = order.ValidationWindowExpiresAt.Value, Detail = "Buyer validation window closes." });
        }

        if (order.Dispute != null)
            timeline.Add(new TimelineEntry { Event = "DisputeRaised", Timestamp = order.Dispute.CreatedAt, Detail = $"Dispute raised: {order.Dispute.Reason}" });

        if (order.CompletedAt.HasValue)
            timeline.Add(new TimelineEntry { Event = "Completed", Timestamp = order.CompletedAt.Value, Detail = "Escrow released to merchant." });

        return Result<List<TimelineEntry>>.Success(timeline.OrderBy(t => t.Timestamp).ToList());
    }
}
