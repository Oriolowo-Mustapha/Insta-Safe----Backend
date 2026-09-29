using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Features.Admin.Commands.RetryRiderPayout;

/// <summary>
/// Re-runs a rider fee transfer that failed at delivery confirmation.
/// The handover already happened (the order is Delivered) - only the money
/// needs a second attempt. Mirrors RetryPayout, which covers the vendor leg.
/// Never pays twice: refuses when a driver transfer reference already exists.
/// </summary>
public class RetryRiderPayoutCommandHandler : IRequestHandler<RetryRiderPayoutCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IPaystackClient _paystack;
    private readonly IMapper _mapper;
    private readonly ILogger<RetryRiderPayoutCommandHandler> _logger;

    public RetryRiderPayoutCommandHandler(
        IOrderRepository orders, IPaystackClient paystack,
        IMapper mapper, ILogger<RetryRiderPayoutCommandHandler> logger)
    {
        _orders = orders; _paystack = paystack; _mapper = mapper; _logger = logger;
    }

    public async Task<Result<OrderDto>> Handle(RetryRiderPayoutCommand req, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(req.OrderId, ct);
        if (order is null)
            return Result<OrderDto>.Failure("Order not found.");
        if (order.Status is not OrderStatus.Delivered)
            throw new ConflictException($"Cannot retry a rider payout from status {order.Status}.");
        if (order.DeliveryFeeKobo <= 0)
            return Result<OrderDto>.Failure("This order carries no rider fee.");
        if (!string.IsNullOrWhiteSpace(order.DriverTransferReference))
            throw new ConflictException("This order already has a rider transfer reference.");
        if (string.IsNullOrWhiteSpace(order.DriverRecipientCode))
            throw new ConflictException("Rider has no payout recipient. Fix the rider bank details first.");

        string? transferRef;
        try
        {
            transferRef = await _paystack.InitiateTransferAsync(
                order.DeliveryFeeKobo, order.DriverRecipientCode, $"InstaSafe rider fee {order.Id}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rider payout retry call failed for order {OrderId}; outcome unknown", order.Id);
            return Result<OrderDto>.Failure(
                "Paystack transfer call failed and the outcome is unknown. Check the Paystack transfers list before retrying.");
        }

        if (string.IsNullOrWhiteSpace(transferRef))
            return Result<OrderDto>.Failure(
                "Paystack rejected the transfer. Check your Paystack balance and the rider's recipient code.");

        order.DriverTransferReference = transferRef;
        order.Touch();
        await _orders.SaveAsync(ct);

        return Result<OrderDto>.Success(_mapper.Map<OrderDto>(order));
    }
}
