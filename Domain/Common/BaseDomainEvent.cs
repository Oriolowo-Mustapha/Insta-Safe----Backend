using MediatR;

namespace InstaSafe.Domain.Common;

public abstract record BaseDomainEvent(DateTimeOffset OccurredOnUtc) : INotification
{
    protected BaseDomainEvent() : this(DateTimeOffset.UtcNow) { }
}
