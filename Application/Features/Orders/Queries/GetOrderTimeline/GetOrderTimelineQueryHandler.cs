using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Enums;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Queries.GetOrderTimeline;

public class GetOrderTimelineQueryHandler : IRequestHandler<GetOrderTimelineQuery, Result<OrderTimelineDto>>
{
    private readonly IOrderRepository _orders;

    public GetOrderTimelineQueryHandler(IOrderRepository orders) => _orders = orders;

    public async Task<Result<OrderTimelineDto>> Handle(GetOrderTimelineQuery req, CancellationToken ct)
    {
        var reference = (req.Reference ?? "").Trim();
        Domain.Entities.Order? order = null;
        if (Guid.TryParse(reference, out var id))
            order = await _orders.GetByIdAsync(id, ct);
        order ??= await _orders.GetByPaystackRefAsync(reference, ct);
        if (order is null)
            return Result<OrderTimelineDto>.Failure("Order not found.");

        var events = new List<TimelineEventDto>
        {
            new("created", "Order created — payment link sent", order.CreatedAt),
            new("payment_pending", "Waiting for buyer payment", order.CreatedAt)
        };

        if (order.HeldAt is not null)
            events.Add(new TimelineEventDto("funds_held",
                $"Payment received — {_money(order.AmountKobo)} held in escrow", order.HeldAt));

        if (order.Fulfillment == FulfillmentType.Dispatch && order.DeliveredAt is not null)
            events.Add(new TimelineEventDto("delivered",
                $"Delivered — buyer has until {(order.ReleaseDueAt?.ToString("dd MMM HH:mm") ?? "?")} to inspect or dispute",
                order.DeliveredAt));

        if (order.Status == OrderStatus.Released)
            events.Add(new TimelineEventDto("released",
                $"Completed — funds released{(order.TransferReference is null ? "" : $" ({order.TransferReference})")}",
                order.ReleasedAt));
        else if (order.Status == OrderStatus.Refunded)
            events.Add(new TimelineEventDto("refunded",
                $"Refunded to buyer{(order.RefundReference is null ? "" : $" ({order.RefundReference})")}",
                order.UpdatedAt));
        else if (order.Status == OrderStatus.Disputed)
            events.Add(new TimelineEventDto("disputed",
                $"Disputed — funds frozen{(string.IsNullOrWhiteSpace(order.DisputeReason) ? "" : $": {order.DisputeReason}")}",
                order.UpdatedAt));

        return Result<OrderTimelineDto>.Success(new OrderTimelineDto(
            order.Id,
            order.PaystackReference ?? order.Id.ToString(),
            order.Status.ToString(),
            order.AmountKobo,
            events));
    }

    private static string _money(long kobo) => $"₦{kobo / 100:N0}";
}
