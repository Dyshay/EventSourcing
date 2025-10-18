using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public class OrderShippedEvent : DomainEvent
{
    public string ShippingAddress { get; init; }
    public string TrackingNumber { get; init; }

    public OrderShippedEvent(string shippingAddress, string trackingNumber)
    {
        ShippingAddress = shippingAddress;
        TrackingNumber = trackingNumber;
    }
}
