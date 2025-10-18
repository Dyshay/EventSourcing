namespace EventSourcing.Example.Api.Models;

public record ShipOrderRequest(
    string ShippingAddress,
    string TrackingNumber
);
