using AutoMapper;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Notifications;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using InstaSafe.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Features.Dispatch.Commands.ConfirmDelivery;

public class ConfirmDeliveryCommandHandler : IRequestHandler<ConfirmDeliveryCommand, Result<OrderDto>>
{
    public static readonly TimeSpan InspectionWindow = TimeSpan.FromHours(24);

    private readonly IOrderRepository _orders;
    private readonly IAppDbContext _db;
    private readonly IOtpService _otp;
    private readonly IPaystackClient _paystack;
    private readonly OrderNotifier _notifier;
    private readonly IMapper _mapper;

    public ConfirmDeliveryCommandHandler(
        IOrderRepository orders, IAppDbContext db, IOtpService otp,
        IPaystackClient paystack, OrderNotifier notifier, IMapper mapper)
    {
        _orders = orders; _db = db; _otp = otp;
        _paystack = paystack; _notifier = notifier; _mapper = mapper;
    }

    public async Task<Result<OrderDto>> Handle(ConfirmDeliveryCommand req, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(req.OrderId, ct);
        if (order is null)
            return Result<OrderDto>.Failure("Order not found.");
        if (order.Fulfillment != FulfillmentType.Dispatch)
            return Result<OrderDto>.Failure("This order needs no dispatch.");
        if (order.Status != OrderStatus.Held)
            throw new ConflictException($"Order is {order.Status}, delivery not expected.");
        if (string.IsNullOrWhiteSpace(order.DriverPhone)
            || PhoneNormalizer.Normalize(order.DriverPhone) != PhoneNormalizer.Normalize(req.DriverPhone))
            return Result<OrderDto>.Failure("This delivery is not assigned to you.");
        if (order.OtpHash is null || !order.OtpHash.Contains('.'))
            return Result<OrderDto>.Failure("No delivery code pending for this order.");
        if (order.OtpExpiresAt is null || DateTimeOffset.UtcNow > order.OtpExpiresAt)
            throw new DomainValidationException("Delivery code has expired.");
        if (order.OtpAttempts >= 5)
            throw new ForbiddenAccessException("Too many attempts. Order locked.");

        var salt = order.OtpHash.Split('.', 2)[1];
        if (order.OtpHash != _otp.Hash(req.Otp.Trim(), salt) + "." + salt)
        {
            order.OtpAttempts++;
            order.Touch();
            await _orders.SaveAsync(ct);
            throw new DomainValidationException("Invalid delivery code.");
        }

        if (order.DeliveryFeeKobo > 0 && order.DriverTransferReference is null)
        {
            var recipient = order.DriverRecipientCode;
            if (recipient is not null)
            {
                var transferRef = await _paystack.InitiateTransferAsync(
                    order.DeliveryFeeKobo, recipient, $"InstaSafe rider fee {order.Id}", ct);
                order.DriverTransferReference = transferRef;
            }
        }

        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = DateTimeOffset.UtcNow;
        order.ReleaseDueAt = DateTimeOffset.UtcNow.Add(InspectionWindow);
        order.Touch();
        order.AddDomainEvent(new OrderDeliveredEvent(order.Id));

        var ledger = await _db.Ledgers
            .Where(l => l.OrderId == order.Id && l.ReleasedAt == null)
            .FirstOrDefaultAsync(ct);

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "OrderDelivered",
            Payload = System.Text.Json.JsonSerializer.Serialize(new { order.Id, order.ReleaseDueAt })
        });
        await _orders.SaveAsync(ct);

        await _notifier.DeliveredAsync(order, order.BuyerEmail);

        return Result<OrderDto>.Success(_mapper.Map<OrderDto>(order));
    }
}
