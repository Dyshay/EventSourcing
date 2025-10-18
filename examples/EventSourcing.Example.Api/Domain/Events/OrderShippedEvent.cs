using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public record OrderShippedEvent(
    string ShippingAddress,
    string TrackingNumber
) : DomainEvent;
