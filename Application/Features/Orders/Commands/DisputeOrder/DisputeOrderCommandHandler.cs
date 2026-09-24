using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Notifications;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using InstaSafe.Domain.Exceptions;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.DisputeOrder;

public class DisputeOrderCommandHandler : IRequestHandler<DisputeOrderCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly ISanitizer _sanitizer;
    private readonly OrderNotifier _notifier;
    private readonly IMapper _mapper;

    public DisputeOrderCommandHandler(
        IOrderRepository orders, ISanitizer sanitizer, OrderNotifier notifier, IMapper mapper)
    {
        _orders = orders; _sanitizer = sanitizer; _notifier = notifier; _mapper = mapper;
    }

    public async Task<Result<OrderDto>> Handle(DisputeOrderCommand req, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(req.OrderId, ct);
        if (order is null)
            return Result<OrderDto>.Failure("Order not found.");
        if (order.Status is not (OrderStatus.Held or OrderStatus.Delivered))
            throw new ConflictException($"Cannot dispute from status {order.Status}.");

        order.Status = OrderStatus.Disputed;
        order.DisputeReason = _sanitizer.Clean(req.Reason, 1000);
        order.Touch();
        order.AddDomainEvent(new OrderDisputedEvent(order.Id, order.DisputeReason));
        await _orders.SaveAsync(ct);

        await _notifier.DisputeFiledAsync(order, order.DisputeReason);

        return Result<OrderDto>.Success(_mapper.Map<OrderDto>(order));
    }
}
