using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public record UserDeactivatedEvent(
    string Reason
) : DomainEvent;
