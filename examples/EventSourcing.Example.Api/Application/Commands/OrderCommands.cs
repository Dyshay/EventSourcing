using EventSourcing.Core.CQRS;

namespace EventSourcing.Example.Api.Application.Commands;

// Commands - Express intent to change state

public record CreateOrderCommand(
    Guid OrderId,
    Guid CustomerId
) : Command<CommandResult>;

public record AddOrderItemCommand(
    Guid OrderId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
) : Command<CommandResult>;

public record ShipOrderCommand(
    Guid OrderId,
    string ShippingAddress,
    string TrackingNumber
) : Command<CommandResult>;

public record CancelOrderCommand(
    Guid OrderId,
    string Reason
) : Command<CommandResult>;
