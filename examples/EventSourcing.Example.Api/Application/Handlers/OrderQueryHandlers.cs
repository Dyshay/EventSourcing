using EventSourcing.Abstractions;
using EventSourcing.Example.Api.Application.Queries;
using EventSourcing.Example.Api.Domain;
using MediatR;

namespace EventSourcing.Example.Api.Application.Handlers;

/// <summary>
/// Handles GetOrderQuery by loading the aggregate and projecting to DTO.
/// </summary>
public class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderDto?>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;

    public GetOrderQueryHandler(IAggregateRepository<OrderAggregate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<OrderDto?> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);

            return new OrderDto(
                Id: order.Id,
                CustomerId: order.CustomerId,
                Total: order.Total,
                Status: order.Status,
                Items: order.Items.Select(i => new OrderItemDto(
                    i.ProductName,
                    i.Quantity,
                    i.UnitPrice
                )).ToList(),
                ShippingAddress: order.ShippingAddress,
                TrackingNumber: order.TrackingNumber,
                CancellationReason: order.CancellationReason,
                Version: order.Version
            );
        }
        catch (AggregateNotFoundException)
        {
            return null;
        }
    }
}

/// <summary>
/// Handles GetOrderStatusQuery - lightweight query for just status.
/// </summary>
public class GetOrderStatusQueryHandler : IRequestHandler<GetOrderStatusQuery, OrderStatusDto?>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;

    public GetOrderStatusQueryHandler(IAggregateRepository<OrderAggregate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<OrderStatusDto?> Handle(GetOrderStatusQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);

            return new OrderStatusDto(
                OrderId: order.Id,
                Status: order.Status,
                Version: order.Version
            );
        }
        catch (AggregateNotFoundException)
        {
            return null;
        }
    }
}

/// <summary>
/// Handles GetAllowedOrderActionsQuery - useful for UI to show available actions.
/// This demonstrates how state machines help build dynamic UIs.
/// </summary>
public class GetAllowedOrderActionsQueryHandler : IRequestHandler<GetAllowedOrderActionsQuery, OrderActionsDto>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;

    public GetAllowedOrderActionsQueryHandler(IAggregateRepository<OrderAggregate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<OrderActionsDto> Handle(GetAllowedOrderActionsQuery request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);

        // Map allowed state transitions to action names
        var allowedActions = new List<string>();

        if (order.Status == OrderStatus.Pending)
        {
            allowedActions.Add("Ship");
            allowedActions.Add("Cancel");
            allowedActions.Add("AddItem");
        }
        else if (order.Status == OrderStatus.Shipped)
        {
            // Future: Could add "Deliver" action
        }

        return new OrderActionsDto(
            OrderId: order.Id,
            CurrentStatus: order.Status,
            AllowedActions: allowedActions
        );
    }
}
