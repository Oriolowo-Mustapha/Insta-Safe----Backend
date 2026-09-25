using InstaSafe.Domain.Common;
using InstaSafe.Domain.Enums;

namespace InstaSafe.Domain.Entities;

public sealed class OrderItem
{
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public long UnitPriceKobo { get; set; }
}

public class Order : BaseEntity
{
    public Guid? VendorId { get; set; }
    public Vendor? Vendor { get; set; }
    public string VendorPhone { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? BuyerEmail { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public long AmountKobo { get; set; }
    public string Currency { get; set; } = "NGN";
    public OrderStatus Status { get; set; } = OrderStatus.Draft;

    public string? PaystackReference { get; set; }
    public string? PaystackAuthUrl { get; set; }
    public string? VendorRecipientCode { get; set; }

    public string? OtpHash { get; set; }
    public DateTimeOffset? OtpExpiresAt { get; set; }
    public int OtpAttempts { get; set; }
    public DateTimeOffset? HeldAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? TransferReference { get; set; }
    public string? RefundReference { get; set; }
    public string? PaystackCustomerCode { get; set; }
    public string? PayVirtualAccountNumber { get; set; }
    public string? PayVirtualAccountBank { get; set; }
    public string? PayVirtualAccountName { get; set; }

    public FulfillmentType Fulfillment { get; set; } = FulfillmentType.Dispatch;
    public long DeliveryFeeKobo { get; set; }
    public Guid? DriverId { get; set; }
    public Dispatcher? Driver { get; set; }
    public string? DriverPhone { get; set; }
    public string? DriverAccountNumber { get; set; }
    public string? DriverBankCode { get; set; }
    public string? DriverRecipientCode { get; set; }
    public string? DriverTransferReference { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ReleaseDueAt { get; set; }
    public string? DisputeReason { get; set; }
}
