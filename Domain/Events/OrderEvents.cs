using InstaSafe.Domain.Common;

namespace InstaSafe.Domain.Events;

public sealed record OrderCreatedEvent(Guid OrderId) : BaseDomainEvent;
public sealed record FundsHeldEvent(Guid OrderId, long AmountKobo) : BaseDomainEvent;
public sealed record FundsReleasedEvent(Guid OrderId, long AmountKobo, string? TransferRef) : BaseDomainEvent;
public sealed record OrderRefundedEvent(Guid OrderId) : BaseDomainEvent;
public sealed record OrderDeliveredEvent(Guid OrderId) : BaseDomainEvent;
public sealed record OrderDisputedEvent(Guid OrderId, string Reason) : BaseDomainEvent;
