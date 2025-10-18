using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal Total
) : DomainEvent;
