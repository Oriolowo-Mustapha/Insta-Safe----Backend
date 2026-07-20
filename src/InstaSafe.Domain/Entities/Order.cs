using InstaSafe.Domain.Common;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;

namespace InstaSafe.Domain.Entities;

public class Order : BaseEntity
{
    public required string OrderReference { get; init; }
    public Guid MerchantId { get; init; }
    public Guid? BuyerId { get; private set; }
    public required string ItemName { get; init; }
    public string? ItemDescription { get; init; }
    public string? ItemImageUrl { get; init; }
    public decimal Price { get; init; }
    public string Currency { get; init; } = "NGN";
    public string? DeliveryAddress { get; init; }
    public string? DispatcherPhone { get; init; }
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;
    public int RiskScore { get; set; } = 0;
    public string RiskLevel { get; set; } = "Unknown";
    public string? EscrowLinkUrl { get; private set; }
    public DateTime? EscrowLinkExpiresAt { get; private set; }
    public DateTime? FundedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? ValidationWindowExpiresAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public virtual Merchant Merchant { get; private set; } = null!;
    public virtual Buyer? Buyer { get; private set; }
    public virtual EscrowTransaction? EscrowTransaction { get; private set; }
    public virtual DeliverySession? DeliverySession { get; private set; }
    public virtual Dispute? Dispute { get; private set; }
    public virtual PayoutSplit? PayoutSplit { get; private set; }

    public void SetBuyer(Guid buyerId)
    {
        BuyerId = buyerId;
    }

    public void GenerateEscrowLink(string url, DateTime expiresAt)
    {
        EscrowLinkUrl = url;
        EscrowLinkExpiresAt = expiresAt;
        Status = OrderStatus.PendingPayment;
        AddDomainEvent(new EscrowLinkGeneratedEvent(Id, url));
    }

    public void MarkAsPendingPayment()
    {
        Status = OrderStatus.PendingPayment;
    }

    public void MarkAsFunded(DateTime fundedAt)
    {
        Status = OrderStatus.FundedInEscrow;
        FundedAt = fundedAt;
    }

    public void Dispatch()
    {
        Status = OrderStatus.Dispatched;
    }

    public void Deliver(DateTime deliveredAt, DateTime validationWindowExpiresAt, Guid sessionId)
    {
        Status = OrderStatus.Delivered;
        DeliveredAt = deliveredAt;
        ValidationWindowExpiresAt = validationWindowExpiresAt;
        AddDomainEvent(new OrderDeliveredEvent(Id, sessionId));
    }

    public void MarkAsDisputed(Guid disputeId)
    {
        Status = OrderStatus.Disputed;
        AddDomainEvent(new DisputeRaisedEvent(Id, disputeId));
    }

    public void Expire()
    {
        Status = OrderStatus.Expired;
        AddDomainEvent(new OrderExpiredEvent(Id));
    }

    public void Refund(decimal refundAmount, DateTime refundedAt)
    {
        Status = OrderStatus.Refunded;
        CompletedAt = refundedAt;
        AddDomainEvent(new OrderRefundedEvent(Id, refundAmount));
    }

    public void ReleaseFunds(decimal merchantAmount, decimal platformCommission, DateTime releasedAt)
    {
        Status = OrderStatus.CompletedReleased;
        CompletedAt = releasedAt;
        AddDomainEvent(new EscrowReleasedEvent(Id, merchantAmount, platformCommission));
    }
}
