using EventSourcing.Abstractions;
using EventSourcing.Core.CQRS;
using EventSourcing.Example.Api.Application.Commands;
using EventSourcing.Example.Api.Domain;
using MediatR;

namespace EventSourcing.Example.Api.Application.Handlers;

/// <summary>
/// Handles CreateOrderCommand by creating a new OrderAggregate and saving it.
/// </summary>
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, CommandResult>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;

    public CreateOrderCommandHandler(IAggregateRepository<OrderAggregate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CommandResult> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Create new aggregate
        var order = new OrderAggregate();
        order.CreateOrder(request.OrderId, request.CustomerId);

        // Save to repository (appends events)
        await _repository.SaveAsync(order, cancellationToken);

        return new CommandResult(
            AggregateId: request.OrderId.ToString(),
            Version: order.Version
        );
    }
}

/// <summary>
/// Handles AddOrderItemCommand by loading the order, adding an item, and saving.
/// </summary>
public class AddOrderItemCommandHandler : IRequestHandler<AddOrderItemCommand, CommandResult>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;

    public AddOrderItemCommandHandler(IAggregateRepository<OrderAggregate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CommandResult> Handle(AddOrderItemCommand request, CancellationToken cancellationToken)
    {
        // Load existing aggregate
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);

        // Execute business logic
        order.AddItem(request.ProductName, request.Quantity, request.UnitPrice);

        // Save changes
        await _repository.SaveAsync(order, cancellationToken);

        return new CommandResult(
            AggregateId: request.OrderId.ToString(),
            Version: order.Version
        );
    }
}

/// <summary>
/// Handles ShipOrderCommand with state machine validation.
/// </summary>
public class ShipOrderCommandHandler : IRequestHandler<ShipOrderCommand, CommandResult>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;

    public ShipOrderCommandHandler(IAggregateRepository<OrderAggregate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CommandResult> Handle(ShipOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);

        // State machine will validate if shipping is allowed
        order.Ship(request.ShippingAddress, request.TrackingNumber);

        await _repository.SaveAsync(order, cancellationToken);

        return new CommandResult(
            AggregateId: request.OrderId.ToString(),
            Version: order.Version
        );
    }
}

/// <summary>
/// Handles CancelOrderCommand.
/// </summary>
public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, CommandResult>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;

    public CancelOrderCommandHandler(IAggregateRepository<OrderAggregate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CommandResult> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);

        order.Cancel(request.Reason);

        await _repository.SaveAsync(order, cancellationToken);

        return new CommandResult(
            AggregateId: request.OrderId.ToString(),
            Version: order.Version
        );
    }
}
