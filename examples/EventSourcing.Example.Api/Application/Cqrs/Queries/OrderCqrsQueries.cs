using EventSourcing.CQRS.Queries;
using EventSourcing.Example.Api.Application.DTOs;

namespace EventSourcing.Example.Api.Application.Cqrs.Queries;

/// <summary>
/// CQRS Query to get an order by ID
/// </summary>
public record GetOrderCqrsQuery : IQuery<OrderDto?>
{
    public Guid QueryId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }

    public Guid OrderId { get; init; }
}

public record GetOrderStatusCqrsQuery : IQuery<OrderStatusDto?>
{
    public Guid QueryId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }

    public Guid OrderId { get; init; }
}

public record GetAllowedOrderActionsCqrsQuery : IQuery<OrderActionsDto>
{
    public Guid QueryId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }

    public Guid OrderId { get; init; }
}

/// <summary>
/// Temporal query to get order state at a specific point in time
/// </summary>
public record GetOrderAsOfDateQuery : ITemporalQuery<OrderDto?>
{
    public Guid QueryId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }

    public Guid OrderId { get; init; }
    public DateTimeOffset AsOfDate { get; init; }
}
