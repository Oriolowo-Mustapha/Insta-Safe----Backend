namespace InstaSafe.Domain.Common;

public abstract record BaseDomainEvent(DateTimeOffset OccurredOnUtc)
{
    protected BaseDomainEvent() : this(DateTimeOffset.UtcNow) { }
}
