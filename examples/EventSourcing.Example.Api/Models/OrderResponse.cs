using EventSourcing.Example.Api.Domain;

namespace EventSourcing.Example.Api.Models;

public record OrderResponse(
    Guid Id,
    Guid CustomerId,
    decimal Total,
    List<OrderItemResponse> Items,
    OrderStatus Status,
    string? ShippingAddress,
    string? TrackingNumber,
    string? CancellationReason,
    int Version
);

public record OrderItemResponse(
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal
);
