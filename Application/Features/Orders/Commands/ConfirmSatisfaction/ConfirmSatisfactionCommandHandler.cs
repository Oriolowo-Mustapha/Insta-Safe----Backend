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

namespace InstaSafe.Application.Features.Orders.Commands.ConfirmSatisfaction;

public class ConfirmSatisfactionCommandHandler : IRequestHandler<ConfirmSatisfactionCommand, Result<PublicOrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IAppDbContext _db;
    private readonly IPaystackClient _paystack;
    private readonly OrderNotifier _notifier;
    private readonly IMapper _mapper;

    public ConfirmSatisfactionCommandHandler(
        IOrderRepository orders, IAppDbContext db, IPaystackClient paystack,
        OrderNotifier notifier, IMapper mapper)
    {
        _orders = orders; _db = db; _paystack = paystack; _notifier = notifier; _mapper = mapper;
    }

    public async Task<Result<PublicOrderDto>> Handle(ConfirmSatisfactionCommand req, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(req.OrderId, ct);
        if (order is null)
            return Result<PublicOrderDto>.Failure("Order not found.");
        if (order.Fulfillment != FulfillmentType.Digital)
            return Result<PublicOrderDto>.Failure("Only digital orders use buyer confirmation.");
        if (order.Status != OrderStatus.Held)
            throw new ConflictException($"Order is {order.Status}, confirmation not expected.");

        var remainder = OrderReleaseCalculator.VendorRemainderKobo(order);
        string? transferRef = null;
        if (order.VendorRecipientCode is not null && remainder > 0)
            transferRef = await _paystack.InitiateTransferAsync(
                remainder, order.VendorRecipientCode, $"InstaSafe payout {order.Id}", ct);

        order.Status = OrderStatus.Released;
        order.ReleasedAt = DateTimeOffset.UtcNow;
        order.TransferReference = transferRef;
        order.Touch();
        order.AddDomainEvent(new FundsReleasedEvent(order.Id, remainder, transferRef));

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

        return Result<PublicOrderDto>.Success(_mapper.Map<PublicOrderDto>(order));
    }
}
