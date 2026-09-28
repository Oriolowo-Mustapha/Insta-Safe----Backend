using InstaSafe.Domain.Enums;

namespace InstaSafe.Application.Features.Orders.DTOs;

/// <summary>
/// Order shape for the rider portal (driver JWT). A rider needs to find the
/// buyer and complete the drop, and nothing about the money behind it.
///
/// Omitted: amountKobo (escrow — the rider never handles the order value),
/// vendorPhone, buyerEmail, every Paystack/transfer/refund reference,
/// payVirtualAccountNumber/Bank, paystackAuthUrl, driverTransferReference.
///
/// Kept: customerPhone + deliveryAddress (the rider's actual job) and
/// deliveryFeeKobo (their own payout).
/// </summary>
public sealed record DispatchOrderDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    FulfillmentType Fulfillment,
    string CustomerName,
    string CustomerPhone,
    string DeliveryAddress,
    long DeliveryFeeKobo,
    string Currency,
    List<OrderItemDto> Items,
    string? DriverPhone,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? ReleaseDueAt);
