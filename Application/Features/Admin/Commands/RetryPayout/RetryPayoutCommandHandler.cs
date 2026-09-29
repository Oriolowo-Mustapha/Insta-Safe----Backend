using AutoMapper;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Features.Admin.Commands.RetryPayout;

public class RetryPayoutCommandHandler : IRequestHandler<RetryPayoutCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IAppDbContext _db;
    private readonly IPaystackClient _paystack;
    private readonly IMapper _mapper;
    private readonly ILogger<RetryPayoutCommandHandler> _logger;

    public RetryPayoutCommandHandler(
        IOrderRepository orders, IAppDbContext db, IPaystackClient paystack,
        IMapper mapper, ILogger<RetryPayoutCommandHandler> logger)
    {
        _orders = orders; _db = db; _paystack = paystack; _mapper = mapper; _logger = logger;
    }

    public async Task<Result<OrderDto>> Handle(RetryPayoutCommand req, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(req.OrderId, ct);
        if (order is null)
            return Result<OrderDto>.Failure("Order not found.");
        if (order.Status is not OrderStatus.Released)
            throw new ConflictException($"Cannot retry payout from status {order.Status}.");
        if (!string.IsNullOrWhiteSpace(order.TransferReference))
            throw new ConflictException("This order already has a transfer reference.");
        if (string.IsNullOrWhiteSpace(order.VendorRecipientCode))
            throw new ConflictException("Vendor has no payout recipient. Set payout details first.");

        var remainder = OrderReleaseCalculator.VendorRemainderKobo(order);
        if (remainder <= 0)
            return Result<OrderDto>.Failure("Nothing left to pay out on this order.");

        string? transferRef;
        try
        {
            transferRef = await _paystack.InitiateTransferAsync(
                remainder, order.VendorRecipientCode, $"InstaSafe payout {order.Id}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payout retry call failed for order {OrderId}; outcome unknown", order.Id);
            return Result<OrderDto>.Failure(
                "Paystack transfer call failed and the outcome is unknown. Check the Paystack transfers list before retrying.");
        }

        if (string.IsNullOrWhiteSpace(transferRef))
            return Result<OrderDto>.Failure(
                "Paystack rejected the transfer. Check your Paystack balance and the vendor's recipient code.");

        order.TransferReference = transferRef;
        order.ReleasedAt ??= DateTimeOffset.UtcNow;
        order.Touch();

        var ledger = await _db.Ledgers.FirstOrDefaultAsync(l => l.OrderId == order.Id, ct);
        if (ledger is not null && string.IsNullOrWhiteSpace(ledger.TransferReference))
            ledger.TransferReference = transferRef;

        await _orders.SaveAsync(ct);

        return Result<OrderDto>.Success(_mapper.Map<OrderDto>(order));
    }
}
