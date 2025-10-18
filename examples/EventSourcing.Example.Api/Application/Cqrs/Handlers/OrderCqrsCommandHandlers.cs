using EventSourcing.Abstractions;
using EventSourcing.CQRS.Commands;
using EventSourcing.Example.Api.Application.Cqrs.Commands;
using EventSourcing.Example.Api.Domain;
using EventSourcing.Example.Api.Domain.Events;

namespace EventSourcing.Example.Api.Application.Cqrs.Handlers;

public class CreateOrderCqrsCommandHandler
    : ICommandHandler<CreateOrderCqrsCommand, OrderCreatedEvent>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;
    private readonly ILogger<CreateOrderCqrsCommandHandler> _logger;

    public CreateOrderCqrsCommandHandler(
        IAggregateRepository<OrderAggregate, Guid> repository,
        ILogger<CreateOrderCqrsCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CommandResult<OrderCreatedEvent>> HandleAsync(
        CreateOrderCqrsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating order for customer {CustomerId}",
            command.CustomerId);

        var orderId = Guid.NewGuid();
        var order = new OrderAggregate();
        order.CreateOrder(orderId, command.CustomerId);

        await _repository.SaveAsync(order, cancellationToken);

        var @event = order.UncommittedEvents
            .OfType<OrderCreatedEvent>()
            .First();

        return CommandResult<OrderCreatedEvent>.SuccessResult(
            @event,
            aggregateId: orderId,
            version: order.Version);
    }
}

public class AddOrderItemCqrsCommandHandler
    : ICommandHandler<AddOrderItemCqrsCommand, OrderItemAddedEvent>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;
    private readonly ILogger<AddOrderItemCqrsCommandHandler> _logger;

    public AddOrderItemCqrsCommandHandler(
        IAggregateRepository<OrderAggregate, Guid> repository,
        ILogger<AddOrderItemCqrsCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CommandResult<OrderItemAddedEvent>> HandleAsync(
        AddOrderItemCqrsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Adding item to order {OrderId}",
            command.OrderId);

        var order = await _repository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order == null)
        {
            return CommandResult<OrderItemAddedEvent>.FailureResult(
                $"Order {command.OrderId} not found");
        }

        order.AddItem(command.ProductName, command.Quantity, command.UnitPrice);

        await _repository.SaveAsync(order, cancellationToken);

        var @event = order.UncommittedEvents
            .OfType<OrderItemAddedEvent>()
            .Last();

        return CommandResult<OrderItemAddedEvent>.SuccessResult(
            @event,
            aggregateId: command.OrderId,
            version: order.Version);
    }
}

public class ShipOrderCqrsCommandHandler
    : ICommandHandler<ShipOrderCqrsCommand, OrderShippedEvent>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;
    private readonly ILogger<ShipOrderCqrsCommandHandler> _logger;

    public ShipOrderCqrsCommandHandler(
        IAggregateRepository<OrderAggregate, Guid> repository,
        ILogger<ShipOrderCqrsCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CommandResult<OrderShippedEvent>> HandleAsync(
        ShipOrderCqrsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Shipping order {OrderId}",
            command.OrderId);

        var order = await _repository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order == null)
        {
            return CommandResult<OrderShippedEvent>.FailureResult(
                $"Order {command.OrderId} not found");
        }

        order.Ship(command.ShippingAddress, command.TrackingNumber);

        await _repository.SaveAsync(order, cancellationToken);

        var @event = order.UncommittedEvents
            .OfType<OrderShippedEvent>()
            .First();

        return CommandResult<OrderShippedEvent>.SuccessResult(
            @event,
            aggregateId: command.OrderId,
            version: order.Version);
    }
}

public class CancelOrderCqrsCommandHandler
    : ICommandHandler<CancelOrderCqrsCommand, OrderCancelledEvent>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;
    private readonly ILogger<CancelOrderCqrsCommandHandler> _logger;

    public CancelOrderCqrsCommandHandler(
        IAggregateRepository<OrderAggregate, Guid> repository,
        ILogger<CancelOrderCqrsCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CommandResult<OrderCancelledEvent>> HandleAsync(
        CancelOrderCqrsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Cancelling order {OrderId}",
            command.OrderId);

        var order = await _repository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order == null)
        {
            return CommandResult<OrderCancelledEvent>.FailureResult(
                $"Order {command.OrderId} not found");
        }

        order.Cancel(command.Reason);

        await _repository.SaveAsync(order, cancellationToken);

        var @event = order.UncommittedEvents
            .OfType<OrderCancelledEvent>()
            .First();

        return CommandResult<OrderCancelledEvent>.SuccessResult(
            @event,
            aggregateId: command.OrderId,
            version: order.Version);
    }
}
