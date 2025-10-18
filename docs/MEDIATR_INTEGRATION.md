## MediatR Integration with State Machines and Event Sourcing

This document explains how to integrate MediatR with Event Sourcing and State Machines to build a complete CQRS application.

## Architecture Overview

```
┌─────────────┐
│  API Layer  │  (Controllers)
└──────┬──────┘
       │ Send Commands/Queries
       ▼
┌─────────────┐
│   MediatR   │  (Mediator Pattern)
└──────┬──────┘
       │ Routes to Handlers
       ▼
┌──────────────────────────────┐
│  Command/Query Handlers      │
└──────┬───────────────────────┘
       │ Uses
       ▼
┌──────────────────────────────┐
│  Aggregates + State Machines │  (Domain Logic)
└──────┬───────────────────────┘
       │ Raises Events
       ▼
┌──────────────────────────────┐
│  Event Store + Snapshots     │  (Persistence)
└──────────────────────────────┘
       │
       │ Publishes Notifications
       ▼
┌──────────────────────────────┐
│  Notification Handlers       │  (Side Effects)
└──────────────────────────────┘
```

## Key Components

### 1. Commands (Write Operations)

Commands express **intent to change state**. They are imperative.

```csharp
// Base command type
public abstract record Command<TResult> : IRequest<TResult>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

// Concrete command
public record CreateOrderCommand(
    Guid OrderId,
    Guid CustomerId
) : Command<CommandResult>;

public record ShipOrderCommand(
    Guid OrderId,
    string ShippingAddress,
    string TrackingNumber
) : Command<CommandResult>;
```

**Command Naming**: Use imperative verbs (`CreateOrder`, `ShipOrder`, `CancelOrder`)

### 2. Queries (Read Operations)

Queries retrieve data **without modifying state**.

```csharp
// Base query type
public abstract record Query<TResult> : IRequest<TResult>;

// Concrete queries
public record GetOrderQuery(Guid OrderId) : Query<OrderDto?>;

public record GetAllowedOrderActionsQuery(Guid OrderId) : Query<OrderActionsDto>;
```

**Query Naming**: Use descriptive questions (`GetOrder`, `GetOrderStatus`, `GetAllowedActions`)

### 3. Command Handlers

Handlers orchestrate the workflow: load aggregate → execute business logic → save.

```csharp
public class ShipOrderCommandHandler : IRequestHandler<ShipOrderCommand, CommandResult>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;

    public ShipOrderCommandHandler(IAggregateRepository<OrderAggregate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CommandResult> Handle(ShipOrderCommand request, CancellationToken cancellationToken)
    {
        // 1. Load aggregate (from events + snapshots)
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);

        // 2. Execute business logic (validates via state machine)
        order.Ship(request.ShippingAddress, request.TrackingNumber);

        // 3. Save (appends new events)
        await _repository.SaveAsync(order, cancellationToken);

        return new CommandResult(
            AggregateId: request.OrderId.ToString(),
            Version: order.Version
        );
    }
}
```

**Handler Responsibilities**:
- ✅ Orchestration (load, execute, save)
- ✅ Error handling
- ❌ NO business logic (belongs in aggregate)

### 4. Query Handlers

Query handlers load aggregates and project to DTOs.

```csharp
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

            // Project aggregate to DTO
            return new OrderDto(
                Id: order.Id,
                CustomerId: order.CustomerId,
                Total: order.Total,
                Status: order.Status,
                Items: order.Items.Select(i => new OrderItemDto(
                    i.ProductName, i.Quantity, i.UnitPrice
                )).ToList(),
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

**Note**: For read-heavy scenarios, consider read models/projections instead of loading aggregates.

### 5. State Machine with MediatR

State machines can publish notifications when transitions occur.

```csharp
public class OrderAggregate : AggregateBase<Guid>
{
    private readonly StateMachineWithMediatr<OrderStatus> _stateMachine;

    public OrderAggregate(IMediator mediator)
    {
        _stateMachine = new StateMachineWithMediatr<OrderStatus>(
            initialState: OrderStatus.Pending,
            mediator: mediator,
            aggregateType: nameof(OrderAggregate),
            getAggregateId: () => Id.ToString()
        );

        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        _stateMachine
            .Allow(OrderStatus.Pending, OrderStatus.Shipped, OrderStatus.Cancelled);
    }

    public async Task ShipAsync(string address, string tracking)
    {
        // Validate business rules
        if (Items.Count == 0)
            throw new InvalidOperationException("Cannot ship empty order");

        RaiseEvent(new OrderShippedEvent(address, tracking));

        // Transition with MediatR notification
        await _stateMachine.TransitionToAsync(OrderStatus.Shipped);
    }
}
```

### 6. Notification Handlers (Reactive Workflows)

React to state transitions with side effects.

```csharp
public class OrderShippedNotificationHandler
    : INotificationHandler<StateTransitionNotification<OrderStatus>>
{
    private readonly IEmailService _emailService;
    private readonly ILogger _logger;

    public async Task Handle(
        StateTransitionNotification<OrderStatus> notification,
        CancellationToken cancellationToken)
    {
        // Only react to Shipped transitions
        if (notification.ToState != OrderStatus.Shipped)
            return;

        _logger.LogInformation(
            "Order {OrderId} shipped. Sending notification",
            notification.AggregateId);

        // Send email
        await _emailService.SendOrderShippedEmail(
            notification.AggregateId,
            cancellationToken);

        // Update external tracking system
        await _trackingService.RegisterShipment(
            notification.AggregateId,
            cancellationToken);
    }
}
```

**Multiple handlers** can react to the same transition:
- Email service
- SMS notifications
- Analytics tracking
- External system integration
- Audit logging

## Complete Example: Ship an Order

### 1. API Controller

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
    public async Task<ActionResult<CommandResult>> ShipOrder(
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

    [HttpGet("{orderId}/actions")]
    public async Task<ActionResult<OrderActionsDto>> GetAllowedActions(Guid orderId)
    {
        var query = new GetAllowedOrderActionsQuery(orderId);
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
```

### 2. Request Flow

```
1. POST /api/orders/123/ship
   ↓
2. MediatR routes to ShipOrderCommandHandler
   ↓
3. Handler loads OrderAggregate from repository
   ↓
4. OrderAggregate.Ship() executes business logic
   ↓
5. State machine validates transition (Pending → Shipped)
   ↓
6. Event OrderShippedEvent is raised
   ↓
7. Repository saves events to event store
   ↓
8. State machine publishes StateTransitionNotification
   ↓
9. Notification handlers react (email, analytics, etc.)
   ↓
10. Response returned to client
```

## Configuration (Dependency Injection)

```csharp
// Program.cs or Startup.cs

var builder = WebApplication.CreateBuilder(args);

// Register MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Register Event Sourcing
builder.Services.AddEventSourcing(options =>
{
    options.UseMongoDB(
        builder.Configuration.GetConnectionString("MongoDB")!,
        "EventSourcingDb"
    );
    options.SnapshotFrequency = 10;
});

// Register repositories
builder.Services.AddScoped<IAggregateRepository<OrderAggregate, Guid>,
    AggregateRepository<OrderAggregate, Guid>>();

var app = builder.Build();
```

## Query: Get Allowed Actions

This query is particularly useful for building dynamic UIs.

```csharp
// Query
public record GetAllowedOrderActionsQuery(Guid OrderId) : Query<OrderActionsDto>;

// DTO
public record OrderActionsDto(
    Guid OrderId,
    OrderStatus CurrentStatus,
    List<string> AllowedActions
);

// Handler
public class GetAllowedOrderActionsQueryHandler
    : IRequestHandler<GetAllowedOrderActionsQuery, OrderActionsDto>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;

    public async Task<OrderActionsDto> Handle(
        GetAllowedOrderActionsQuery request,
        CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);

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

// Frontend can then:
GET /api/orders/123/actions
→ { "currentStatus": "Pending", "allowedActions": ["Ship", "Cancel", "AddItem"] }
→ UI shows only Ship, Cancel, and AddItem buttons
```

## Benefits of This Architecture

### 1. **Separation of Concerns**
- Controllers: Route HTTP requests
- Handlers: Orchestrate workflows
- Aggregates: Business logic
- State Machines: Transition rules
- Notifications: Side effects

### 2. **Testability**
Each component can be tested in isolation:

```csharp
[Fact]
public async Task ShipOrderHandler_ValidOrder_ShouldSucceed()
{
    // Arrange
    var mockRepo = new Mock<IAggregateRepository<OrderAggregate, Guid>>();
    var order = CreateTestOrder();
    mockRepo.Setup(r => r.GetByIdAsync(order.Id, default))
        .ReturnsAsync(order);

    var handler = new ShipOrderCommandHandler(mockRepo.Object);
    var command = new ShipOrderCommand(order.Id, "123 Main St", "TRACK123");

    // Act
    var result = await handler.Handle(command, default);

    // Assert
    result.AggregateId.Should().Be(order.Id.ToString());
    mockRepo.Verify(r => r.SaveAsync(order, default), Times.Once);
}
```

### 3. **Scalability**
- Commands and queries can scale independently
- Notification handlers run asynchronously
- Read models can be optimized separately

### 4. **Reactive Workflows**
State transitions automatically trigger workflows:
- Shipped → Send email + Update tracking
- Cancelled → Refund payment
- Pending → Send reminder after 24h

### 5. **Type Safety**
Compile-time validation of:
- Command/query signatures
- Handler registrations
- State transitions

## Advanced Patterns

### Validation Pipeline

Use MediatR behaviors for cross-cutting concerns:

```csharp
public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var failures = _validators
            .Select(v => v.Validate(request))
            .SelectMany(result => result.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
```

### Idempotency

Ensure commands can be safely retried:

```csharp
public record Command : IRequest
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
}

// In handler:
if (await _commandStore.WasProcessed(command.CommandId))
{
    return await _commandStore.GetResult(command.CommandId);
}

// Process command...

await _commandStore.SaveResult(command.CommandId, result);
```

## Summary

MediatR + Event Sourcing + State Machines creates a powerful architecture:

| Component | Responsibility |
|-----------|----------------|
| **MediatR** | Message routing (commands, queries, notifications) |
| **Commands** | Intent to change state |
| **Queries** | Read data without side effects |
| **Aggregates** | Business logic and invariants |
| **State Machines** | Valid state transitions |
| **Events** | Facts that happened |
| **Notification Handlers** | React to state changes |

This architecture gives you:
- ✅ Clean separation of concerns
- ✅ Fully testable components
- ✅ Reactive workflows
- ✅ Audit trail (events)
- ✅ Time travel (replay events)
- ✅ Dynamic UIs (query allowed actions)
