using AutoMapper;
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

namespace InstaSafe.Application.Features.Orders.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IAppDbContext _db;
    private readonly IOtpService _otp;
    private readonly IPaystackClient _paystack;
    private readonly OrderNotifier _notifier;
    private readonly IMapper _mapper;

    public VerifyOtpCommandHandler(
        IOrderRepository orders, IAppDbContext db,
        IOtpService otp, IPaystackClient paystack, OrderNotifier notifier, IMapper mapper)
    {
        _orders = orders; _db = db; _otp = otp; _paystack = paystack; _notifier = notifier; _mapper = mapper;
    }

    public async Task<Result<OrderDto>> Handle(VerifyOtpCommand req, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(req.OrderId, ct);
        if (order is null)
            return Result<OrderDto>.Failure("Order not found.");
        if (order.Status != OrderStatus.Held)
            throw new ConflictException($"Order is {order.Status}, OTP not expected.");
        if (order.Fulfillment == FulfillmentType.Dispatch && !string.IsNullOrWhiteSpace(order.DriverPhone))
            return Result<OrderDto>.Failure("This order has an assigned dispatcher — confirm delivery from the driver portal.");
        if (order.OtpHash is null || !order.OtpHash.Contains('.'))
            return Result<OrderDto>.Failure("No OTP pending for this order.");
        if (order.OtpExpiresAt is null || DateTimeOffset.UtcNow > order.OtpExpiresAt)
            throw new DomainValidationException("OTP has expired.");
        if (order.OtpAttempts >= 5)
            throw new ForbiddenAccessException("Too many OTP attempts. Order locked.");

        var salt = order.OtpHash.Split('.', 2)[1];
        if (order.OtpHash != _otp.Hash(req.Otp.Trim(), salt) + "." + salt)
        {
            order.OtpAttempts++;
            order.Touch();
            await _orders.SaveAsync(ct);
            throw new DomainValidationException("Invalid OTP.");
        }

        string? transferRef = null;
        if (order.VendorRecipientCode is not null)
            transferRef = await _paystack.InitiateTransferAsync(
                order.AmountKobo, order.VendorRecipientCode, $"InstaSafe payout {order.Id}", ct);
        order.Status = OrderStatus.Released;
        order.ReleasedAt = DateTimeOffset.UtcNow;
        order.TransferReference = transferRef;
        order.Touch();
        order.AddDomainEvent(new FundsReleasedEvent(order.Id, order.AmountKobo, transferRef));

        var ledger = await _db.Ledgers
            .Where(l => l.OrderId == order.Id && l.ReleasedAt == null)
            .FirstOrDefaultAsync(ct);
        if (ledger is not null)
        {
            ledger.ReleasedAt = DateTimeOffset.UtcNow;
            ledger.TransferReference = transferRef;
        }

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "FundsReleased",
            Payload = System.Text.Json.JsonSerializer.Serialize(new { order.Id, transferRef })
        });
        await _orders.SaveAsync(ct);

        await _notifier.ReleasedAsync(order, order.BuyerEmail, transferRef);

        return Result<OrderDto>.Success(_mapper.Map<OrderDto>(order));
    }
}
