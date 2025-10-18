using EventSourcing.Core.CQRS;
using EventSourcing.Example.Api.Domain;

namespace EventSourcing.Example.Api.Application.Queries;

// Queries - Retrieve data without modifying state

public record GetOrderQuery(Guid OrderId) : Query<OrderDto?>;

public record GetOrderStatusQuery(Guid OrderId) : Query<OrderStatusDto?>;

public record GetAllowedOrderActionsQuery(Guid OrderId) : Query<OrderActionsDto>;

// DTOs - Data Transfer Objects for queries

public record OrderDto(
    Guid Id,
    Guid CustomerId,
    decimal Total,
    OrderStatus Status,
    List<OrderItemDto> Items,
    string? ShippingAddress,
    string? TrackingNumber,
    string? CancellationReason,
    int Version
);

public record OrderItemDto(
    string ProductName,
    int Quantity,
    decimal UnitPrice
);

public record OrderStatusDto(
    Guid OrderId,
    OrderStatus Status,
    int Version
);

public record OrderActionsDto(
    Guid OrderId,
    OrderStatus CurrentStatus,
    List<string> AllowedActions
);
