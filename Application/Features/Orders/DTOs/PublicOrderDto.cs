using InstaSafe.Domain.Enums;

namespace InstaSafe.Application.Features.Orders.DTOs;

/// <summary>
/// Order shape for anonymous callers: the public track page and the guest
/// actions it triggers. The order number is the only credential on those
/// endpoints, so this DTO deliberately omits anything sensitive enough to
/// weaponise a leaked link:
///
/// Omitted: vendorPhone, customerPhone, buyerEmail, paystackReference,
/// paystackAuthUrl, transferReference, refundReference,
/// payVirtualAccountNumber/Bank, driverPhone, driverTransferReference.
///
/// Kept: the order id (guest actions post to /{id}) and the buyer's own
/// name/address, which the buyer needs to recognise their order.
///
/// Authenticated surfaces (vendor dashboard, admin console) keep OrderDto.
/// </summary>
public sealed record PublicOrderDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    FulfillmentType Fulfillment,
    long AmountKobo,
    long DeliveryFeeKobo,
    string Currency,
    string CustomerName,
    string DeliveryAddress,
    List<OrderItemDto> Items,
    DateTimeOffset? HeldAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? ReleaseDueAt,
    DateTimeOffset? ReleasedAt,
    string? DisputeReason);
