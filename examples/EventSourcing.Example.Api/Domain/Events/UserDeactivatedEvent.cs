using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public class UserDeactivatedEvent : DomainEvent
{
    public string Reason { get; init; }

    public UserDeactivatedEvent(string reason)
    {
        Reason = reason;
    }
}
