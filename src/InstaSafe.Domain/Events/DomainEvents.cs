using InstaSafe.Domain.Common;

namespace InstaSafe.Domain.Events;

public record OrderCreatedEvent(Guid OrderId) : IDomainEvent;
public record EscrowLinkGeneratedEvent(Guid OrderId, string EscrowLinkUrl) : IDomainEvent;
public record OrderFundedEvent(Guid OrderId, string MonnifyTransactionReference) : IDomainEvent;
public record OrderDispatchedEvent(Guid OrderId, Guid DeliverySessionId) : IDomainEvent;
public record OrderDeliveredEvent(Guid OrderId, Guid DeliverySessionId) : IDomainEvent;
public record DisputeRaisedEvent(Guid OrderId, Guid DisputeId) : IDomainEvent;
public record EscrowReleasedEvent(Guid OrderId, decimal MerchantAmount, decimal PlatformCommission) : IDomainEvent;
public record OrderRefundedEvent(Guid OrderId, decimal RefundAmount) : IDomainEvent;
public record OrderExpiredEvent(Guid OrderId) : IDomainEvent;
