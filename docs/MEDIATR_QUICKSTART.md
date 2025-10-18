# MediatR + State Machines + Event Sourcing - Quick Start

## Installation

MediatR is already installed. If you're creating a new project:

```bash
dotnet add package MediatR
```

## Minimal Example: Create and Ship an Order

### 1. Define a Command

```csharp
using EventSourcing.Core.CQRS;

public record ShipOrderCommand(
    Guid OrderId,
    string ShippingAddress,
    string TrackingNumber
) : Command<CommandResult>;
```

### 2. Create the Handler

```csharp
using EventSourcing.Abstractions;
using MediatR;

public class ShipOrderCommandHandler : IRequestHandler<ShipOrderCommand, CommandResult>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;

    public ShipOrderCommandHandler(IAggregateRepository<OrderAggregate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CommandResult> Handle(ShipOrderCommand request, CancellationToken ct)
    {
        // Load the aggregate
        var order = await _repository.GetByIdAsync(request.OrderId, ct);

        // Execute business logic (validates via state machine)
        order.Ship(request.ShippingAddress, request.TrackingNumber);

        // Save
        await _repository.SaveAsync(order, ct);

        return new CommandResult(request.OrderId.ToString(), order.Version);
    }
}
```

### 3. Use in a Controller

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("{orderId}/ship")]
    public async Task<ActionResult> ShipOrder(
        Guid orderId,
        [FromBody] ShipOrderRequest request)
    {
        var command = new ShipOrderCommand(
            orderId,
            request.ShippingAddress,
            request.TrackingNumber
        );

        var result = await _mediator.Send(command);

        return Ok(result);
    }
}
```

## Queries: Reading Data

### 1. Define the Query and DTO

```csharp
public record GetOrderQuery(Guid OrderId) : Query<OrderDto?>;

public record OrderDto(
    Guid Id,
    OrderStatus Status,
    decimal Total,
    int Version
);
```

### 2. Create the Handler

```csharp
public class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderDto?>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;

    public GetOrderQueryHandler(IAggregateRepository<OrderAggregate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<OrderDto?> Handle(GetOrderQuery request, CancellationToken ct)
    {
        try
        {
            var order = await _repository.GetByIdAsync(request.OrderId, ct);

            return new OrderDto(
                Id: order.Id,
                Status: order.Status,
                Total: order.Total,
                Version: order.Version
            );
        }
        catch (AggregateNotFoundException)
        {
            return null;
        }
    }
}
```

### 3. Use in a Controller

```csharp
[HttpGet("{orderId}")]
public async Task<ActionResult<OrderDto>> GetOrder(Guid orderId)
{
    var query = new GetOrderQuery(orderId);
    var result = await _mediator.Send(query);

    return result != null ? Ok(result) : NotFound();
}
```

## React to State Changes with Notifications

### 1. Notification Handler

```csharp
using EventSourcing.Core.StateMachine;

public class OrderShippedNotificationHandler
    : INotificationHandler<StateTransitionNotification<OrderStatus>>
{
    private readonly ILogger<OrderShippedNotificationHandler> _logger;

    public OrderShippedNotificationHandler(ILogger<OrderShippedNotificationHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(
        StateTransitionNotification<OrderStatus> notification,
        CancellationToken ct)
    {
        if (notification.ToState != OrderStatus.Shipped)
            return Task.CompletedTask;

        _logger.LogInformation(
            "📦 Order {OrderId} shipped! Sending email...",
            notification.AggregateId);

        // Send email, SMS, etc.
        return Task.CompletedTask;
    }
}
```

### 2. Use State Machine with MediatR

```csharp
public class OrderAggregate : AggregateBase<Guid>
{
    private readonly StateMachineWithMediatr<OrderStatus> _stateMachine;

    public OrderAggregate(IMediator? mediator = null)
    {
        _stateMachine = new StateMachineWithMediatr<OrderStatus>(
            initialState: OrderStatus.Pending,
            mediator: mediator,
            aggregateType: nameof(OrderAggregate),
            getAggregateId: () => Id.ToString()
        );

        _stateMachine.Allow(OrderStatus.Pending, OrderStatus.Shipped, OrderStatus.Cancelled);
    }

    public async Task ShipAsync(string address, string tracking)
    {
        RaiseEvent(new OrderShippedEvent(address, tracking));

        // Transition + publish MediatR notification
        await _stateMachine.TransitionToAsync(OrderStatus.Shipped);
    }
}
```

## Configuration (Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Event Sourcing
builder.Services.AddEventSourcing(options =>
{
    options.UseMongoDB(
        builder.Configuration.GetConnectionString("MongoDB")!,
        "EventSourcingDb"
    );
});

// Repositories
builder.Services.AddScoped<IAggregateRepository<OrderAggregate, Guid>,
    AggregateRepository<OrderAggregate, Guid>>();

var app = builder.Build();

app.Run();
```

## Complete Flow

```
POST /api/orders/123/ship
    ↓
Controller sends ShipOrderCommand via MediatR
    ↓
ShipOrderCommandHandler processes the command
    ↓
Loads OrderAggregate from Event Store
    ↓
OrderAggregate.Ship() executes business logic
    ↓
State Machine validates transition (Pending → Shipped)
    ↓
OrderShippedEvent is emitted
    ↓
Repository saves events
    ↓
State Machine publishes StateTransitionNotification
    ↓
Notification handlers react (email, analytics...)
    ↓
Response returned to client
```

## Useful Query: Allowed Actions

This query allows your UI to know which buttons to display:

```csharp
// Query
public record GetAllowedOrderActionsQuery(Guid OrderId) : Query<OrderActionsDto>;

public record OrderActionsDto(
    Guid OrderId,
    OrderStatus CurrentStatus,
    List<string> AllowedActions
);

// Handler
public class GetAllowedOrderActionsQueryHandler
    : IRequestHandler<GetAllowedOrderActionsQuery, OrderActionsDto>
{
    public async Task<OrderActionsDto> Handle(...)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, ct);

        var allowedActions = new List<string>();
        if (order.Status == OrderStatus.Pending)
        {
            allowedActions.AddRange(new[] { "Ship", "Cancel", "AddItem" });
        }

        return new OrderActionsDto(
            OrderId: order.Id,
            CurrentStatus: order.Status,
            AllowedActions: allowedActions
        );
    }
}

// Frontend
GET /api/orders/123/actions
→ { "currentStatus": "Pending", "allowedActions": ["Ship", "Cancel", "AddItem"] }
→ UI displays only Ship, Cancel, and AddItem buttons
```

## Important Patterns

### ✅ DO

```csharp
// Commands are ACTIONS (imperative)
public record ShipOrderCommand(...)
public record CancelOrderCommand(...)

// Queries are QUESTIONS (descriptive)
public record GetOrderQuery(...)
public record GetOrderStatusQuery(...)

// DTOs for responses
public record OrderDto(...) // No business logic

// Business logic in the Aggregate
public class OrderAggregate
{
    public void Ship(...) { /* validation + events */ }
}
```

### ❌ DON'T

```csharp
// ❌ Business logic in the handler
public async Task<Result> Handle(ShipOrderCommand cmd, CancellationToken ct)
{
    var order = await _repo.GetByIdAsync(cmd.OrderId);

    // ❌ NO! Logic should be in the aggregate
    if (order.Status != OrderStatus.Pending)
        throw new Exception("Cannot ship");

    order.ShippingAddress = cmd.Address; // ❌ NO!

    // ✅ YES! Delegate to aggregate
    order.Ship(cmd.Address, cmd.TrackingNumber);
}

// ❌ Return aggregate directly
public record GetOrderQuery : Query<OrderAggregate> // ❌ NO!
public record GetOrderQuery : Query<OrderDto>      // ✅ YES!
```

## Benefits

1. **Separation of concerns**: Each handler does one thing
2. **Testability**: Independent handlers, easy to mock
3. **Reactive workflows**: Automatic notifications on transitions
4. **Type-safe**: Compilation fails if handlers are missing
5. **Dynamic UI**: Query to know which actions are possible

## Resources

- [Complete MediatR Documentation](./MEDIATR_INTEGRATION.md)
- [State Machines Documentation](./STATE_MACHINES.md)
- Examples: `examples/EventSourcing.Example.Api/Application/`

## Next Steps

1. Create your own commands/queries
2. Implement notification handlers for your workflows
3. Add MediatR behaviors for validation, logging, etc.
4. Build read models for performance (projections)
