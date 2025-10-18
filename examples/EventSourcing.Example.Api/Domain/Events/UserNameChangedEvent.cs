using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public record UserNameChangedEvent(
    string FirstName,
    string LastName
) : DomainEvent;
