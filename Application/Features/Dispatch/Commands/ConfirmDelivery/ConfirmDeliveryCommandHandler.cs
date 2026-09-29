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
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Features.Dispatch.Commands.ConfirmDelivery;

public class ConfirmDeliveryCommandHandler : IRequestHandler<ConfirmDeliveryCommand, Result<DispatchOrderDto>>
{
    public static readonly TimeSpan InspectionWindow = TimeSpan.FromHours(24);

    private readonly IOrderRepository _orders;
    private readonly IAppDbContext _db;
    private readonly IOtpService _otp;
    private readonly IPaystackClient _paystack;
    private readonly OrderNotifier _notifier;
    private readonly IMapper _mapper;
    private readonly ILogger<ConfirmDeliveryCommandHandler> _logger;

    public ConfirmDeliveryCommandHandler(
        IOrderRepository orders, IAppDbContext db, IOtpService otp,
        IPaystackClient paystack, OrderNotifier notifier, IMapper mapper,
        ILogger<ConfirmDeliveryCommandHandler> logger)
    {
        _orders = orders; _db = db; _otp = otp;
        _paystack = paystack; _notifier = notifier; _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<DispatchOrderDto>> Handle(ConfirmDeliveryCommand req, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(req.OrderId, ct);
        if (order is null)
            return Result<DispatchOrderDto>.Failure("Order not found.");
        if (order.Fulfillment != FulfillmentType.Dispatch)
            return Result<DispatchOrderDto>.Failure("This order needs no dispatch.");
        if (order.Status != OrderStatus.Held)
            throw new ConflictException($"Order is {order.Status}, delivery not expected.");
        if (string.IsNullOrWhiteSpace(order.DriverPhone)
            || PhoneNormalizer.Normalize(order.DriverPhone) != PhoneNormalizer.Normalize(req.DriverPhone))
            return Result<DispatchOrderDto>.Failure("This delivery is not assigned to you.");
        if (order.OtpHash is null || !order.OtpHash.Contains('.'))
            return Result<DispatchOrderDto>.Failure("No delivery code pending for this order.");
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

        // The rider's fee moves here, at handover. Either silent skip below used
        // to swallow the failure completely - no log context, no notification,
        // no admin visibility - so an unpaid rider was only discoverable by
        // querying the database. Both now record loudly (Error log + outbox
        // RiderPayoutFailed, which surfaces in the admin outbox errors) and the
        // delivery itself still completes: the handover happened, only the
        // money needs a retry via POST /api/admin/orders/{id}/retry-rider-payout.
        if (order.DeliveryFeeKobo > 0 && order.DriverTransferReference is null)
        {
            var recipient = order.DriverRecipientCode;
            if (recipient is null)
            {
                _logger.LogError(
                    "Rider fee of {FeeKobo} kobo for order {OrderId} ({OrderNumber}) cannot be paid: " +
                    "no driver recipient code. The rider bank details were never verified at order creation.",
                    order.DeliveryFeeKobo, order.Id, order.OrderNumber);
                RecordRiderPayoutFailure(order,
                    "No driver recipient code - rider bank details were never verified at order creation.");
            }
            else
            {
                var recorded = false;
                string? transferRef = null;
                try
                {
                    transferRef = await _paystack.InitiateTransferAsync(
                        order.DeliveryFeeKobo, recipient, $"InstaSafe rider fee {order.Id}", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Rider fee transfer call failed for order {OrderId} ({OrderNumber}); outcome unknown. " +
                        "Check the Paystack transfers list before retrying.",
                        order.Id, order.OrderNumber);
                    RecordRiderPayoutFailure(order,
                        "Transfer call failed and the outcome is unknown - check the Paystack transfers list before retrying.");
                    recorded = true;
                }

                if (transferRef is not null)
                {
                    order.DriverTransferReference = transferRef;
                }
                else if (!recorded)
                {
                    _logger.LogError(
                        "Paystack rejected the rider fee transfer for order {OrderId} ({OrderNumber}): " +
                        "{FeeKobo} kobo to recipient {Recipient}. Common causes: insufficient Paystack balance " +
                        "(just-collected funds may not be settled yet) or a bad recipient. Nothing was sent.",
                        order.Id, order.OrderNumber, order.DeliveryFeeKobo, recipient);
                    RecordRiderPayoutFailure(order,
                        "Paystack rejected the transfer - check the Paystack balance and the rider recipient. Nothing was sent.");
                }
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

        return Result<DispatchOrderDto>.Success(_mapper.Map<DispatchOrderDto>(order));
    }

    /// <summary>
    /// Records an unpaid rider fee where the admin can see it. The outbox
    /// error keeps Attempts at 5 so it lands in the admin outbox RecentErrors
    /// without the publisher retrying money movement on its own. Persisted by
    /// the SaveAsync below together with the delivery.
    /// </summary>
    private void RecordRiderPayoutFailure(Order order, string reason)
    {
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "RiderPayoutFailed",
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                order.Id,
                order.OrderNumber,
                order.DeliveryFeeKobo,
                order.DriverPhone,
                order.DriverRecipientCode,
                reason
            }),
            Attempts = 5,
            Error = reason
        });
    }
}
