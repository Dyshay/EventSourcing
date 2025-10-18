using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public record UserEmailChangedEvent(
    string NewEmail
) : DomainEvent;
