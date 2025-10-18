using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public record OrderItemAddedEvent(
    string ProductName,
    int Quantity,
    decimal UnitPrice
) : DomainEvent;
