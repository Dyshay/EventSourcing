# Event Sourcing for .NET

[![CI Build and Test](https://github.com/Dyshay/EventSourcing/actions/workflows/ci.yml/badge.svg)](https://github.com/Dyshay/EventSourcing/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download)

Production-ready event sourcing library for .NET 9+ with MongoDB. Build CQRS applications with **state machines**, **MediatR integration**, and **automatic event versioning**.

```bash
dotnet add package EventSourcing.MongoDB
```

## Why Use This?

✅ **Complete audit trail** - Every change recorded as immutable events
✅ **Time travel** - Reconstruct state at any point in history
✅ **CQRS ready** - Commands, queries, and reactive workflows
✅ **State machines** - Validate transitions with built-in state machine
✅ **Production ready** - 184+ tests, CI/CD, clean architecture

## Quick Start (3 steps)

### 1. Configure

```csharp
using EventSourcing.MongoDB;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEventSourcing(config =>
{
    config.UseMongoDB("mongodb://localhost:27017", "eventstore")
          .RegisterEventsFromAssembly(typeof(Program).Assembly)
          .InitializeMongoDB("OrderAggregate");

    config.SnapshotEvery(10); // Performance optimization
});
```

### 2. Define Events & Aggregate

```csharp
using EventSourcing.Core;

// Events (past tense, immutable)
public record OrderCreatedEvent(Guid OrderId, Guid CustomerId) : DomainEvent;
public record OrderShippedEvent(string Address, string Tracking) : DomainEvent;

// Aggregate (business logic + state)
public class OrderAggregate : AggregateBase<Guid>
{
    public override Guid Id { get; protected set; }
    public Guid CustomerId { get; protected set; }
    public OrderStatus Status { get; protected set; } = OrderStatus.Pending;

    public void CreateOrder(Guid orderId, Guid customerId)
    {
        if (Id != Guid.Empty)
            throw new InvalidOperationException("Order already exists");

        RaiseEvent(new OrderCreatedEvent(orderId, customerId));
    }

    public void Ship(string address, string tracking)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Cannot ship non-pending order");

        RaiseEvent(new OrderShippedEvent(address, tracking));
    }

    // Event handlers (reconstruct state)
    private void Apply(OrderCreatedEvent e)
    {
        Id = e.OrderId;
        CustomerId = e.CustomerId;
        Status = OrderStatus.Pending;
    }

    private void Apply(OrderShippedEvent e)
    {
        Status = OrderStatus.Shipped;
    }
}

public enum OrderStatus { Pending, Shipped, Cancelled }
```

### 3. Use in Controller

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repo;

    public OrdersController(IAggregateRepository<OrderAggregate, Guid> repo) => _repo = repo;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req)
    {
        var order = new OrderAggregate();
        order.CreateOrder(Guid.NewGuid(), req.CustomerId);
        await _repo.SaveAsync(order);
        return Ok(order);
    }

    [HttpPost("{id}/ship")]
    public async Task<IActionResult> Ship(Guid id, [FromBody] ShipRequest req)
    {
        var order = await _repo.GetByIdAsync(id);
        order.Ship(req.Address, req.Tracking);
        await _repo.SaveAsync(order);
        return Ok();
    }
}
```

**That's it!** You now have event sourcing with complete audit trail and time travel.

---

## Common Use Cases

### Use Case 1: Built-in CQRS Framework

**When:** You want a complete CQRS solution with commands, queries, validation, caching, and performance optimizations built-in.

**Quick Start:**

```csharp
// 1. Configure CQRS Framework with performance mode
builder.Services.AddEventSourcing(config =>
{
    config.UseMongoDB("mongodb://localhost:27017", "eventstore")
          .RegisterEventsFromAssembly(typeof(Program).Assembly);
});

builder.Services.AddCqrs(
    cqrs => cqrs.AddHandlersFromAssembly(typeof(Program).Assembly),
    options: CqrsOptions.HighPerformance() // 49% faster than Default mode
);

// 2. Define Command & Event
public record CreateOrderCommand : ICommand<OrderCreatedEvent>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }

    public Guid CustomerId { get; init; }
}

// 3. Create Command Handler
public class CreateOrderCommandHandler
    : ICommandHandler<CreateOrderCommand, OrderCreatedEvent>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IAggregateRepository<OrderAggregate, Guid> repository,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CommandResult<OrderCreatedEvent>> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating order for customer {CustomerId}", command.CustomerId);

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
            version: order.Version
        );
    }
}

// 4. Add Validation (optional)
public class CreateOrderCommandValidator : ICommandValidator<CreateOrderCommand>
{
    public Task<IEnumerable<string>> ValidateAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (command.CustomerId == Guid.Empty)
            errors.Add("Customer ID is required");

        return Task.FromResult<IEnumerable<string>>(errors);
    }
}

// 5. Use in Controller
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;

    public OrdersController(ICommandBus commandBus, IQueryBus queryBus)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var command = new CreateOrderCommand
        {
            CustomerId = request.CustomerId,
            Metadata = new Dictionary<string, object>
            {
                ["UserId"] = User?.Identity?.Name ?? "Anonymous",
                ["IpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            }
        };

        var result = await _commandBus.SendAsync(command);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return CreatedAtAction(
            nameof(GetOrder),
            new { id = result.AggregateId },
            new
            {
                orderId = result.AggregateId,
                @event = result.Data,
                version = result.Version,
                executionTimeMs = result.ExecutionTimeMs
            });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(Guid id)
    {
        var query = new GetOrderQuery { OrderId = id };

        // Use cache for 5 minutes with sliding expiration
        var cacheOptions = CacheOptions.WithDuration(
            TimeSpan.FromMinutes(5),
            sliding: true
        );

        var order = await _queryBus.SendAsync(query, cacheOptions);

        if (order == null)
            return NotFound();

        return Ok(order);
    }
}
```

**Performance Modes:**

```csharp
// Default: Full observability (audit trail + logging)
CqrsOptions.Default()

// HighPerformance: 49% faster, no audit trail, minimal logging
CqrsOptions.HighPerformance()

// Custom: Fine-tune optimizations
new CqrsOptions
{
    EnableAuditTrail = false,      // Disable CommandContext tracking
    EnableLogging = true,           // Keep logging
    EnableCommandContextPooling = true,  // Use ObjectPool
    EnableTypeCaching = true,       // Cache MakeGenericType results
    CacheQueryResults = true        // Enable IMemoryCache for queries
}
```

**Benchmark Results** (.NET 9.0, Intel i9-13900KF):

| Configuration | Command Time | vs MediatR | Use Case |
|--------------|--------------|------------|----------|
| **MediatR** | 91.6 ns | 1.0x | Minimal overhead baseline |
| **CQRS HighPerf** | 1,034 ns | 11.4x | High-throughput APIs |
| **CQRS Default** | 2,038 ns | 22.5x | Enterprise + compliance (audit trail) |

**✅ HighPerformance mode delivers 49% improvement over Default mode for commands**

**Benefits:**
- ✅ **Built-in validation** - Automatic validation before execution
- ✅ **Query caching** - Automatic caching with sliding/absolute expiration
- ✅ **Audit trail** - Track who, when, and what (Default mode)
- ✅ **Performance modes** - Choose speed vs. observability trade-off
- ✅ **Middleware pipeline** - Extensible for retry, metrics, custom logic
- ✅ **Automatic cache invalidation** - Invalidate by event type

**Query Caching Features:**

```csharp
// Example: Cache order status, invalidate on order events
var cacheOptions = new CacheOptions
{
    CacheKey = $"order-status-{orderId}",
    Duration = TimeSpan.FromMinutes(10),
    InvalidateOnEvents = new[]
    {
        "OrderCreatedEvent",
        "OrderItemAddedEvent",
        "OrderShippedEvent",
        "OrderCancelledEvent"
    }
};

var status = await _queryBus.SendAsync(query, cacheOptions);
```

**Full Example:** See `examples/EventSourcing.Example.Api/` for complete working implementation

---

### Use Case 2: CQRS with MediatR

**When:** You want to separate commands (write) from queries (read) with reactive workflows.

**Quick Start:** [MediatR Quick Start Guide](docs/MEDIATR_QUICKSTART.md)

**Example:**

```csharp
// 1. Add MediatR + Event Publisher
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddEventSourcing(config =>
{
    config.UseMongoDB(...)
          .AddEventPublisher<MediatREventPublisher>(); // Bridge domain → MediatR
});

// 2. Define Command
public record ShipOrderCommand(Guid OrderId, string Address) : Command<CommandResult>;

// 3. Create Handler
public class ShipOrderHandler : IRequestHandler<ShipOrderCommand, CommandResult>
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repo;

    public async Task<CommandResult> Handle(ShipOrderCommand cmd, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(cmd.OrderId, ct);
        order.Ship(cmd.Address, cmd.Tracking);
        await _repo.SaveAsync(order, ct);
        return new CommandResult(cmd.OrderId.ToString(), order.Version);
    }
}

// 4. Use in Controller
[HttpPost("{id}/ship")]
public async Task<IActionResult> Ship(Guid id, ShipRequest req)
{
    var command = new ShipOrderCommand(id, req.Address);
    var result = await _mediator.Send(command);
    return Ok(result);
}
```

**Benefits:** Separation of concerns, testable handlers, reactive workflows.

**Full Guide:** [MediatR Integration](docs/MEDIATR_INTEGRATION.md) | [Architecture](docs/ARCHITECTURE.md)

---

### Use Case 3: State Machines for Validation

**When:** You need to enforce valid state transitions (e.g., can't ship a cancelled order).

**Quick Start:** [State Machine Guide](docs/STATE_MACHINES.md)

**Example:**

```csharp
using EventSourcing.Core.StateMachine;

public class OrderAggregate : AggregateBase<Guid>
{
    private readonly StateMachineWithEvents<OrderStatus> _stateMachine;

    public OrderAggregate()
    {
        _stateMachine = new StateMachineWithEvents<OrderStatus>(
            initialState: OrderStatus.Pending,
            aggregateType: nameof(OrderAggregate),
            getAggregateId: () => Id.ToString(),
            onTransition: (evt) => RaiseEvent(evt) // Emit state transition events
        );

        // Define allowed transitions
        _stateMachine.Allow(OrderStatus.Pending, OrderStatus.Shipped, OrderStatus.Cancelled);
    }

    public void Ship(string address, string tracking)
    {
        RaiseEvent(new OrderShippedEvent(address, tracking));

        // Validates transition Pending → Shipped
        // Throws InvalidStateTransitionException if not allowed
        _stateMachine.TransitionToWithEvent(OrderStatus.Shipped);
    }

    private void Apply(OrderShippedEvent e)
    {
        // SetState = no validation (replay events, trust history)
        _stateMachine.SetState(OrderStatus.Shipped);
    }
}
```

**Benefits:** Invalid transitions blocked, clean domain model, automatic state transition events.

**Full Guide:** [State Machines Documentation](docs/STATE_MACHINES.md)

---

### Use Case 4: Event Versioning & Schema Evolution

**When:** You need to change event structure over time without breaking existing data.

**Example:**

```csharp
// Version 1 (old)
public record UserCreatedEvent(Guid UserId, string Email) : DomainEvent;

// Version 2 (new - added Name field)
public record UserCreatedEventV2(Guid UserId, string Email, string Name) : DomainEvent;

// Upcaster: Transform V1 → V2 automatically
public class UserCreatedEventUpcaster : IEventUpcaster
{
    public Type SourceType => typeof(UserCreatedEvent);
    public Type TargetType => typeof(UserCreatedEventV2);

    public object Upcast(object sourceEvent)
    {
        var e = (UserCreatedEvent)sourceEvent;
        return new UserCreatedEventV2(e.UserId, e.Email, Name: "Unknown");
    }
}

// Register
builder.Services.AddEventSourcing(config =>
{
    config.RegisterUpcaster<UserCreatedEventUpcaster>();
});
```

**Benefits:** Evolve events without migrations, backward compatibility, zero downtime.

**Full Guide:** [Event Versioning & Upcasting](docs/EVENT_VERSIONING.md)

---

### Use Case 5: Distributed Workflows (Sagas)

**When:** You need to coordinate multi-step processes with automatic rollback on failure.

**Example:**

```csharp
// 1. Define saga data
public class OrderProcessingData
{
    public Guid OrderId { get; set; }
    public string? PaymentId { get; set; }
}

// 2. Create steps with compensation
public class ProcessPaymentStep : SagaStepBase<OrderProcessingData>
{
    public override async Task<bool> ExecuteAsync(OrderProcessingData data, CancellationToken ct)
    {
        data.PaymentId = await _paymentService.ChargeAsync(data.OrderId);
        return true;
    }

    public override async Task<bool> CompensateAsync(OrderProcessingData data, CancellationToken ct)
    {
        await _paymentService.RefundAsync(data.PaymentId); // Rollback
        return true;
    }
}

// 3. Execute saga
var data = new OrderProcessingData { OrderId = orderId };
var saga = new Saga<OrderProcessingData>("OrderProcessing", data)
    .AddSteps(
        new ReserveInventoryStep(),
        new ProcessPaymentStep(),
        new ShipOrderStep()
    );

var result = await _sagaOrchestrator.ExecuteAsync(saga);

if (result.Status == SagaStatus.Completed) { /* Success */ }
else if (result.Status == SagaStatus.Compensated) { /* Failed + rolled back */ }
```

**Benefits:** Reliable distributed transactions, automatic compensation, persistence.

**Full Example:** `examples/EventSourcing.Example.Api/Controllers/SagaController.cs`

---

## Documentation

### 📘 Quick Starts
- **[MediatR Quick Start](docs/MEDIATR_QUICKSTART.md)** - CQRS in 5 minutes
- **[State Machines](docs/STATE_MACHINES.md)** - Manage state transitions
- **This README** - Core concepts

### 📚 In-Depth Guides
- **[MediatR Integration](docs/MEDIATR_INTEGRATION.md)** - Commands, queries, notifications (300+ lines)
- **[Architecture Overview](docs/ARCHITECTURE.md)** - Complete system architecture
- **[Event Versioning](docs/EVENT_VERSIONING.md)** - Schema evolution strategies
- **[Custom Providers](docs/CUSTOM_PROVIDERS.md)** - Build SQL Server/PostgreSQL providers

### 🛠️ Operations
- **[Testing Guide](docs/TESTING.md)** - Run tests locally and in CI
- **[GitHub Secrets](docs/GITHUB_SECRETS.md)** - Configure CI/CD

### 📦 Example Application
`examples/EventSourcing.Example.Api/` - Complete REST API with:
- User & Order aggregates
- State machines
- MediatR CQRS
- Saga workflows
- HTTP test files

---

## How It Works

### Event Sourcing Pattern

Instead of storing current state, store **all state changes** as events:

```
Traditional:                  Event Sourcing:
┌───────────────────┐        ┌─────────────────────────┐
│ Order Table       │        │ Events                  │
├───────────────────┤        ├─────────────────────────┤
│ Id: 123           │        │ 1. OrderCreated(123)    │
│ Status: Shipped   │        │ 2. ItemAdded(...)       │
│ Total: 99.99      │        │ 3. OrderShipped(...)    │
└───────────────────┘        └─────────────────────────┘

Current state =              Current state = replay all events
one row                      (OrderCreated + ItemAdded + Shipped)
```

**Benefits:** Audit trail, time travel, event replay for debugging, build multiple read models.

### Snapshots (Performance Optimization)

```csharp
config.SnapshotEvery(10); // Snapshot every 10 events
```

**Without snapshots:** Replay 1000 events (slow)
**With snapshots:** Load snapshot at event 990 + replay 10 events (fast)

---

## Best Practices

### ✅ DO: Event names in past tense
```csharp
OrderCreatedEvent ✅     CreateOrderEvent ❌
OrderShippedEvent ✅     ShipOrderEvent ❌
```

### ✅ DO: Small, focused events
```csharp
OrderShippedEvent(string address) ✅
OrderUpdatedEvent(string? address, string? status, ...) ❌  // Kitchen sink
```

### ✅ DO: Build read models for queries
```csharp
var users = await _userReadModel.GetActiveUsersAsync(); ✅
var users = allAggregates.Where(u => u.IsActive); ❌  // Don't query aggregates
```

### ✅ DO: Handle concurrency with retries
```csharp
for (int i = 0; i < 3; i++)
{
    try
    {
        var order = await _repo.GetByIdAsync(id);
        order.Ship(address, tracking);
        await _repo.SaveAsync(order);
        break;
    }
    catch (ConcurrencyException) when (i < 2)
    {
        await Task.Delay(100); // Retry with backoff
    }
}
```

---

## Testing

```bash
# Run all tests (184 tests)
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

Example test:

```csharp
[Fact]
public async Task Repository_ConcurrentModification_ShouldThrowConcurrencyException()
{
    // Arrange
    var order = new OrderAggregate();
    order.CreateOrder(Guid.NewGuid(), Guid.NewGuid());
    await _repo.SaveAsync(order);

    // Act - Concurrent modifications
    var order1 = await _repo.GetByIdAsync(order.Id);
    var order2 = await _repo.GetByIdAsync(order.Id);

    order1.Ship("Address 1", "TRACK1");
    await _repo.SaveAsync(order1); // Version = 2

    order2.Ship("Address 2", "TRACK2");

    // Assert
    await Assert.ThrowsAsync<ConcurrencyException>(() => _repo.SaveAsync(order2));
}
```

---

## MongoDB Setup

```bash
# Start MongoDB (Docker)
docker run -d -p 27017:27017 mongo:7.0

# Or use MongoDB Atlas (cloud)
# connection string: mongodb+srv://...
```

**Collections created automatically:**
```
orderaggregate_events      - Append-only event log
orderaggregate_snapshots   - Point-in-time snapshots
```

**Indexes created automatically:**
- Events: `{aggregateId, version}` (unique), `{timestamp}`, `{kind}`
- Snapshots: `{aggregateId, aggregateType}` (unique)

---

## Performance Tips

1. ✅ Use snapshots: `config.SnapshotEvery(10)`
2. ✅ Build read models for queries (CQRS pattern)
3. ✅ Call `InitializeMongoDB()` to ensure indexes
4. ✅ Use connection pooling (automatic)
5. ✅ Handle concurrency with retries

---

## Support

- **Documentation:** [docs/](docs/)
- **Issues:** [GitHub Issues](https://github.com/Dyshay/EventSourcing/issues)
- **Discussions:** [GitHub Discussions](https://github.com/Dyshay/EventSourcing/discussions)
- **Example:** `examples/EventSourcing.Example.Api/`

---

## Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Submit a pull request

---

## License

MIT License - see [LICENSE](LICENSE) file for details.

---

**Built with ❤️ for the .NET community**
