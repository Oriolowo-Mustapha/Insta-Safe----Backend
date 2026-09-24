using InstaSafe.Domain.Enums;

namespace InstaSafe.Application.Features.Orders.DTOs;

public sealed record OrderItemDto(string Description, int Quantity, long UnitPriceKobo);
public sealed record OrderDto(
    Guid Id, string VendorPhone, string CustomerName, string CustomerPhone,
    string DeliveryAddress, List<OrderItemDto> Items, long AmountKobo, string Currency,
    OrderStatus Status, string? PaystackReference, string? PaystackAuthUrl,
    DateTimeOffset? HeldAt, DateTimeOffset? ReleasedAt, string? TransferReference,
    string? RefundReference = null,
    FulfillmentType Fulfillment = FulfillmentType.Dispatch,
    long DeliveryFeeKobo = 0,
    string? DriverPhone = null,
    string? DriverTransferReference = null,
    DateTimeOffset? DeliveredAt = null,
    DateTimeOffset? ReleaseDueAt = null,
    string? DisputeReason = null);
