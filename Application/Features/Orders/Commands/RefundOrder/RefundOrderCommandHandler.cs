using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Notifications;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using InstaSafe.Domain.Exceptions;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.RefundOrder;

public class RefundOrderCommandHandler : IRequestHandler<RefundOrderCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IPaystackClient _paystack;
    private readonly OrderNotifier _notifier;
    private readonly IMapper _mapper;

    public RefundOrderCommandHandler(IOrderRepository orders, IPaystackClient paystack, OrderNotifier notifier, IMapper mapper)
    {
        _orders = orders; _paystack = paystack; _notifier = notifier; _mapper = mapper;
    }

    public async Task<Result<OrderDto>> Handle(RefundOrderCommand req, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(req.OrderId, ct);
        if (order is null)
            return Result<OrderDto>.Failure("Order not found.");
        if (order.Status is OrderStatus.Released or OrderStatus.Refunded)
            throw new ConflictException($"Cannot refund from status {order.Status}.");

        if (order.Status is OrderStatus.Held or OrderStatus.Delivered && order.PaystackReference is not null)
        {
            var (success, refundRef, error) =
                await _paystack.RefundTransactionAsync(order.PaystackReference, ct);
            if (!success)
                return Result<OrderDto>.Failure(error ?? "Paystack refund failed.");
            order.RefundReference = refundRef;
        }

        order.Status = OrderStatus.Refunded;
        order.Touch();
        order.AddDomainEvent(new OrderRefundedEvent(order.Id));

        await _orders.SaveAsync(ct);

        await _notifier.RefundedAsync(order, order.BuyerEmail);

        return Result<OrderDto>.Success(_mapper.Map<OrderDto>(order));
    }
}
