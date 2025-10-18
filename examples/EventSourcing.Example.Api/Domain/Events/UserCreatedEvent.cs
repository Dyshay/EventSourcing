using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public record UserCreatedEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName
) : DomainEvent;
