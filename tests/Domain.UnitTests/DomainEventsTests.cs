using InstaSafe.Domain.Events;
using MediatR;

namespace Domain.UnitTests;

public class DomainEventsTests
{
    [Theory]
    [InlineData(typeof(OrderCreatedEvent))]
    [InlineData(typeof(FundsHeldEvent))]
    [InlineData(typeof(FundsReleasedEvent))]
    [InlineData(typeof(OrderRefundedEvent))]
    [InlineData(typeof(OrderDeliveredEvent))]
    [InlineData(typeof(OrderDisputedEvent))]
    public void DomainEvents_AreMediatRNotifications(Type eventType)
    {
        Assert.True(typeof(INotification).IsAssignableFrom(eventType),
            $"{eventType.Name} must implement INotification or AppDbContext publishing throws at runtime");
    }
}
