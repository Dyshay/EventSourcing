using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public class UserNameChangedEvent : DomainEvent
{
    public string FirstName { get; init; }
    public string LastName { get; init; }

    public UserNameChangedEvent(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }
}
