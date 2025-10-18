using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public class OrderCancelledEvent : DomainEvent
{
    public string Reason { get; init; }

    public OrderCancelledEvent(string reason)
    {
        Reason = reason;
    }
}
