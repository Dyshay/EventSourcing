# State Machines in Event Sourcing

This document explains how to implement state machines in your event-sourced aggregates.

## Why State Machines with Event Sourcing?

State machines and Event Sourcing are a perfect match:
- **Events represent transitions**: Each event is a state transition
- **State is derived**: Current state is computed from events
- **Validation**: State machines enforce valid transitions
- **Auditability**: Full history of state changes

## Approach 1: Built-in StateMachine<TState> (Recommended)

We provide a lightweight `StateMachine<TState>` class in `EventSourcing.Core.StateMachine`.

### Features
- ✅ Generic, works with any enum
- ✅ Fluent API for configuration
- ✅ Transition validation
- ✅ OnEnter/OnExit hooks
- ✅ Query allowed transitions
- ✅ No external dependencies

### Example: Order State Machine

```csharp
public enum OrderStatus
{
    Pending,
    Shipped,
    Delivered,
    Cancelled,
    Returned
}

public class OrderAggregate : AggregateBase<Guid>
{
    private readonly StateMachine<OrderStatus> _stateMachine;

    public OrderStatus Status => _stateMachine.CurrentState;

    public OrderAggregate()
    {
        _stateMachine = new StateMachine<OrderStatus>(OrderStatus.Pending);
        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        _stateMachine
            // Define allowed transitions
            .Allow(OrderStatus.Pending, OrderStatus.Shipped, OrderStatus.Cancelled)
            .Allow(OrderStatus.Shipped, OrderStatus.Delivered)
            .Allow(OrderStatus.Delivered, OrderStatus.Returned)

            // Optional: Add hooks
            .OnEnter(OrderStatus.Shipped, () =>
            {
                // Log, validate, etc.
            })
            .OnExit(OrderStatus.Pending, () =>
            {
                // Ensure order has items before shipping
                if (Items.Count == 0)
                    throw new InvalidOperationException("Cannot transition: Order has no items");
            });
    }

    // Commands use the state machine
    public void Ship(string address, string tracking)
    {
        // Validate transition is allowed
        if (!_stateMachine.CanTransitionTo(OrderStatus.Shipped))
            throw new InvalidOperationException(
                $"Cannot ship order in status {Status}");

        // ... business validation ...

        RaiseEvent(new OrderShippedEvent(address, tracking));
    }

    // Event handlers use SetState (no validation during replay)
    private void Apply(OrderShippedEvent @event)
    {
        ShippingAddress = @event.Address;
        TrackingNumber = @event.Tracking;

        // Use SetState during event replay to avoid validation
        _stateMachine.SetState(OrderStatus.Shipped);
    }

    // Get available actions for UI
    public IEnumerable<OrderStatus> GetAllowedNextStates()
    {
        return _stateMachine.GetAllowedTransitions();
    }
}
```

### Key Points

**Two methods for state changes**:
1. `TransitionTo(state)` - Validates transition, executes hooks. Use in **commands**.
2. `SetState(state)` - No validation. Use in **Apply methods** during event replay.

**Why the distinction?**
- During **command execution**: Validate transitions to prevent invalid operations
- During **event replay**: Events are already validated, just reconstruct state

### State Machine Diagram

```
┌─────────┐
│ Pending │
└────┬────┘
     │
     ├─────────────┐
     │             │
     ▼             ▼
┌─────────┐   ┌───────────┐
│ Shipped │   │ Cancelled │
└────┬────┘   └───────────┘
     │
     ▼
┌───────────┐
│ Delivered │
└─────┬─────┘
      │
      ▼
┌──────────┐
│ Returned │
└──────────┘
```

## Approach 2: Using Stateless Library

[Stateless](https://github.com/dotnet-state-machine/stateless) is a popular NuGet package with more advanced features.

### Installation

```bash
dotnet add package Stateless
```

### Example

```csharp
using Stateless;

public class OrderAggregate : AggregateBase<Guid>
{
    private readonly StateMachine<OrderStatus, OrderTrigger> _stateMachine;

    public enum OrderTrigger
    {
        Ship,
        Deliver,
        Cancel,
        Return
    }

    public OrderAggregate()
    {
        _stateMachine = new StateMachine<OrderStatus, OrderTrigger>(OrderStatus.Pending);
        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        _stateMachine.Configure(OrderStatus.Pending)
            .Permit(OrderTrigger.Ship, OrderStatus.Shipped)
            .Permit(OrderTrigger.Cancel, OrderStatus.Cancelled);

        _stateMachine.Configure(OrderStatus.Shipped)
            .Permit(OrderTrigger.Deliver, OrderStatus.Delivered)
            .OnEntry(() => Console.WriteLine("Order shipped!"));

        _stateMachine.Configure(OrderStatus.Delivered)
            .Permit(OrderTrigger.Return, OrderStatus.Returned);
    }

    public void Ship(string address, string tracking)
    {
        // Check if trigger is allowed
        if (!_stateMachine.CanFire(OrderTrigger.Ship))
            throw new InvalidOperationException($"Cannot ship order in status {_stateMachine.State}");

        // Validate business rules
        if (Items.Count == 0)
            throw new InvalidOperationException("Cannot ship empty order");

        RaiseEvent(new OrderShippedEvent(address, tracking));
    }

    private void Apply(OrderShippedEvent @event)
    {
        ShippingAddress = @event.Address;
        TrackingNumber = @event.Tracking;

        // Fire trigger during replay (Stateless has no "SetState")
        // You may need to use reflection or configure permissive transitions for replay
        _stateMachine.Fire(OrderTrigger.Ship);
    }
}
```

### Stateless Features

- ✅ Parameterized triggers
- ✅ Hierarchical states
- ✅ Entry/Exit actions
- ✅ Guard conditions
- ✅ DOT graph export
- ❌ Requires external dependency
- ⚠️  More complex for simple cases

## Approach 3: State Pattern

For very complex state logic, use the classic State Pattern.

### Example

```csharp
public interface IOrderState
{
    OrderStatus Status { get; }
    void Ship(OrderAggregate order, string address, string tracking);
    void Cancel(OrderAggregate order, string reason);
}

public class PendingState : IOrderState
{
    public OrderStatus Status => OrderStatus.Pending;

    public void Ship(OrderAggregate order, string address, string tracking)
    {
        // Validate and raise event
        order.RaiseEvent(new OrderShippedEvent(address, tracking));
    }

    public void Cancel(OrderAggregate order, string reason)
    {
        order.RaiseEvent(new OrderCancelledEvent(reason));
    }
}

public class ShippedState : IOrderState
{
    public OrderStatus Status => OrderStatus.Shipped;

    public void Ship(OrderAggregate order, string address, string tracking)
    {
        throw new InvalidOperationException("Order already shipped");
    }

    public void Cancel(OrderAggregate order, string reason)
    {
        throw new InvalidOperationException("Cannot cancel shipped order");
    }
}

public class OrderAggregate : AggregateBase<Guid>
{
    private IOrderState _state;

    public OrderAggregate()
    {
        _state = new PendingState();
    }

    public void Ship(string address, string tracking)
    {
        _state.Ship(this, address, tracking);
    }

    private void Apply(OrderShippedEvent @event)
    {
        ShippingAddress = @event.Address;
        _state = new ShippedState();
    }
}
```

### State Pattern Features

- ✅ Very explicit
- ✅ Each state is a class
- ✅ Easy to test individual states
- ❌ Verbose (many classes)
- ❌ Overkill for simple state machines

## Comparison

| Feature | Built-in StateMachine | Stateless | State Pattern |
|---------|----------------------|-----------|---------------|
| Complexity | Low | Medium | High |
| Code volume | Small | Medium | Large |
| External dependency | No | Yes | No |
| Learning curve | Easy | Medium | Easy |
| Features | Basic | Advanced | Custom |
| Best for | Most cases | Complex workflows | Very complex logic |

## Best Practices

### 1. Use SetState in Apply methods

```csharp
// ❌ DON'T use TransitionTo in Apply
private void Apply(OrderShippedEvent @event)
{
    _stateMachine.TransitionTo(OrderStatus.Shipped); // Validates during replay!
}

// ✅ DO use SetState in Apply
private void Apply(OrderShippedEvent @event)
{
    _stateMachine.SetState(OrderStatus.Shipped); // No validation
}
```

### 2. Validate in Commands

```csharp
public void Ship(string address, string tracking)
{
    // ✅ Validate before raising event
    if (!_stateMachine.CanTransitionTo(OrderStatus.Shipped))
        throw new InvalidOperationException("Cannot ship");

    RaiseEvent(new OrderShippedEvent(address, tracking));
}
```

### 3. Initialize State Machine in Constructor

```csharp
public OrderAggregate()
{
    _stateMachine = new StateMachine<OrderStatus>(OrderStatus.Pending);
    ConfigureStateMachine(); // Define transitions
}
```

### 4. Expose Allowed Transitions for UI

```csharp
public IEnumerable<OrderStatus> GetAllowedNextStates()
{
    return _stateMachine.GetAllowedTransitions();
}
```

This allows your UI to show only valid actions to users.

## Testing State Machines

```csharp
[Fact]
public void Ship_WithPendingOrder_ShouldTransitionToShipped()
{
    // Arrange
    var order = new OrderAggregate();
    order.CreateOrder(Guid.NewGuid(), Guid.NewGuid());
    order.AddItem("Widget", 1, 10.00m);

    // Act
    order.Ship("123 Main St", "TRACK123");

    // Assert
    order.Status.Should().Be(OrderStatus.Shipped);
}

[Fact]
public void Ship_WithShippedOrder_ShouldThrowException()
{
    // Arrange
    var order = new OrderAggregate();
    order.CreateOrder(Guid.NewGuid(), Guid.NewGuid());
    order.AddItem("Widget", 1, 10.00m);
    order.Ship("123 Main St", "TRACK123");

    // Act
    var act = () => order.Ship("456 Oak Ave", "TRACK456");

    // Assert
    act.Should().Throw<InvalidOperationException>()
        .WithMessage("*Cannot ship*");
}

[Fact]
public void GetAllowedNextStates_WithPendingOrder_ShouldReturnShippedAndCancelled()
{
    // Arrange
    var order = new OrderAggregate();
    order.CreateOrder(Guid.NewGuid(), Guid.NewGuid());

    // Act
    var allowedStates = order.GetAllowedNextStates();

    // Assert
    allowedStates.Should().Contain(OrderStatus.Shipped);
    allowedStates.Should().Contain(OrderStatus.Cancelled);
}
```

## Conclusion

For most Event Sourcing use cases, the **built-in StateMachine<TState>** is the recommended approach:
- Simple and lightweight
- No external dependencies
- Works well with event replay
- Sufficient for 90% of use cases

Use **Stateless** when you need advanced features like hierarchical states or parameterized triggers.

Use **State Pattern** when state logic is extremely complex and benefits from object-oriented decomposition.
