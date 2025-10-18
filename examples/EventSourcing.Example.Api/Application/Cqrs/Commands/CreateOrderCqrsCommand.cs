using EventSourcing.CQRS.Commands;
using EventSourcing.Example.Api.Domain;
using EventSourcing.Example.Api.Domain.Events;

namespace EventSourcing.Example.Api.Application.Cqrs.Commands;

/// <summary>
/// CQRS Command to create a new order
/// </summary>
public record CreateOrderCqrsCommand : ICommand<OrderCreatedEvent>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }

    public Guid CustomerId { get; init; }
}

public record AddOrderItemCqrsCommand : ICommand<OrderItemAddedEvent>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }

    public Guid OrderId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}

public record ShipOrderCqrsCommand : ICommand<OrderShippedEvent>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }

    public Guid OrderId { get; init; }
    public string ShippingAddress { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
}

public record CancelOrderCqrsCommand : ICommand<OrderCancelledEvent>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }

    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
