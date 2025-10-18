using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public class UserEmailChangedEvent : DomainEvent
{
    public string NewEmail { get; init; }

    public UserEmailChangedEvent(string newEmail)
    {
        NewEmail = newEmail;
    }
}
