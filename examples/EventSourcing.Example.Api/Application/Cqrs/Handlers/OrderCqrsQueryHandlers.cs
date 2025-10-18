using EventSourcing.Abstractions;
using EventSourcing.CQRS.Queries;
using EventSourcing.Example.Api.Application.Cqrs.Queries;
using EventSourcing.Example.Api.Application.DTOs;
using EventSourcing.Example.Api.Domain;

namespace EventSourcing.Example.Api.Application.Cqrs.Handlers;

public class GetOrderCqrsQueryHandler : IQueryHandler<GetOrderCqrsQuery, OrderDto?>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;
    private readonly ILogger<GetOrderCqrsQueryHandler> _logger;

    public GetOrderCqrsQueryHandler(
        IAggregateRepository<OrderAggregate, Guid> repository,
        ILogger<GetOrderCqrsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<OrderDto?> HandleAsync(
        GetOrderCqrsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying order {OrderId}", query.OrderId);

        var order = await _repository.GetByIdAsync(query.OrderId, cancellationToken);
        if (order == null)
            return null;

        return new OrderDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            ShippingAddress = order.ShippingAddress,
            Status = order.Status.ToString(),
            Items = order.Items.Select(i => new OrderItemDto
            {
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList(),
            Total = order.Total,
            TrackingNumber = order.TrackingNumber
        };
    }
}

public class GetOrderStatusCqrsQueryHandler : IQueryHandler<GetOrderStatusCqrsQuery, OrderStatusDto?>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;
    private readonly ILogger<GetOrderStatusCqrsQueryHandler> _logger;

    public GetOrderStatusCqrsQueryHandler(
        IAggregateRepository<OrderAggregate, Guid> repository,
        ILogger<GetOrderStatusCqrsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<OrderStatusDto?> HandleAsync(
        GetOrderStatusCqrsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying status for order {OrderId}", query.OrderId);

        var order = await _repository.GetByIdAsync(query.OrderId, cancellationToken);
        if (order == null)
            return null;

        return new OrderStatusDto
        {
            OrderId = order.Id,
            Status = order.Status.ToString(),
            TrackingNumber = order.TrackingNumber
        };
    }
}

public class GetAllowedOrderActionsCqrsQueryHandler
    : IQueryHandler<GetAllowedOrderActionsCqrsQuery, OrderActionsDto>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;
    private readonly ILogger<GetAllowedOrderActionsCqrsQueryHandler> _logger;

    public GetAllowedOrderActionsCqrsQueryHandler(
        IAggregateRepository<OrderAggregate, Guid> repository,
        ILogger<GetAllowedOrderActionsCqrsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<OrderActionsDto> HandleAsync(
        GetAllowedOrderActionsCqrsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Querying allowed actions for order {OrderId}",
            query.OrderId);

        var order = await _repository.GetByIdAsync(query.OrderId, cancellationToken);
        if (order == null)
        {
            return new OrderActionsDto
            {
                OrderId = query.OrderId,
                AllowedActions = new List<string>()
            };
        }

        var allowedActions = new List<string>();

        if (order.Status == OrderStatus.Pending)
        {
            allowedActions.Add("AddItem");
            allowedActions.Add("Ship");
            allowedActions.Add("Cancel");
        }
        else if (order.Status == OrderStatus.Shipped)
        {
            allowedActions.Add("Cancel");
        }

        return new OrderActionsDto
        {
            OrderId = order.Id,
            AllowedActions = allowedActions
        };
    }
}

public class GetOrderAsOfDateQueryHandler : IQueryHandler<GetOrderAsOfDateQuery, OrderDto?>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;
    private readonly ILogger<GetOrderAsOfDateQueryHandler> _logger;

    public GetOrderAsOfDateQueryHandler(
        IAggregateRepository<OrderAggregate, Guid> repository,
        ILogger<GetOrderAsOfDateQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<OrderDto?> HandleAsync(
        GetOrderAsOfDateQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Querying order {OrderId} as of {AsOfDate}",
            query.OrderId,
            query.AsOfDate);

        // This is a simplified implementation
        // In a real scenario, you'd query events up to the AsOfDate
        var order = await _repository.GetByIdAsync(query.OrderId, cancellationToken);
        if (order == null)
            return null;

        // Note: For a true temporal query, you would need to:
        // 1. Load events from event store up to the AsOfDate
        // 2. Replay them to reconstruct the aggregate at that point in time
        // This is left as an exercise to show the concept

        return new OrderDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            ShippingAddress = order.ShippingAddress,
            Status = order.Status.ToString(),
            Items = order.Items.Select(i => new OrderItemDto
            {
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList(),
            Total = order.Total,
            TrackingNumber = order.TrackingNumber
        };
    }
}
