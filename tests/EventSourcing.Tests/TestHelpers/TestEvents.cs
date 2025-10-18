using EventSourcing.Core;

namespace EventSourcing.Tests.TestHelpers;

public record TestAggregateCreatedEvent(
    Guid Id,
    string Name,
    string Email
) : DomainEvent;

public record TestAggregateRenamedEvent(
    string NewName
) : DomainEvent;

public record TestAggregateEmailChangedEvent(
    string NewEmail
) : DomainEvent;

public record TestAggregateCounterIncrementedEvent : DomainEvent;
