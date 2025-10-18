using EventSourcing.Core;
using EventSourcing.Core.StateMachine;
using EventSourcing.Example.Api.Domain.Events;

namespace EventSourcing.Example.Api.Domain;

/// <summary>
/// Enhanced OrderAggregate using StateMachine for state transitions.
/// Demonstrates how to integrate state machine with Event Sourcing.
/// </summary>
public class OrderAggregateWithStateMachine : AggregateBase<Guid>
{
    private readonly StateMachine<OrderStatus> _stateMachine;

    public override Guid Id { get; protected set; }
    public Guid CustomerId { get; protected set; }
    public decimal Total { get; protected set; }
    public List<OrderItem> Items { get; protected set; } = new();
    public OrderStatus Status => _stateMachine.CurrentState;
    public string? ShippingAddress { get; protected set; }
    public string? TrackingNumber { get; protected set; }
    public string? CancellationReason { get; protected set; }

    public OrderAggregateWithStateMachine()
    {
        // Initialize state machine with allowed transitions
        _stateMachine = new StateMachine<OrderStatus>(OrderStatus.Pending);

        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        // Define allowed transitions
        _stateMachine
            .Allow(OrderStatus.Pending, OrderStatus.Shipped, OrderStatus.Cancelled);
            // Note: Could add more states like Delivered, Returned in the future

        // OnEnter/OnExit hooks (optional - for logging, side effects, etc.)
        _stateMachine.OnEnter(OrderStatus.Shipped, () =>
        {
            // Could log, send notifications, etc.
            // Note: Keep side effects minimal in aggregates
        });

        _stateMachine.OnExit(OrderStatus.Pending, () =>
        {
            // Could validate that order has items before leaving pending
        });
    }

    // Commands - Business logic that raises events

    public void CreateOrder(Guid orderId, Guid customerId)
    {
        if (Id != Guid.Empty)
            throw new InvalidOperationException("Order already exists");

        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer ID is required", nameof(customerId));

        RaiseEvent(new OrderCreatedEvent(orderId, customerId, 0));
    }

    public void AddItem(string productName, int quantity, decimal unitPrice)
    {
        if (Id == Guid.Empty)
            throw new InvalidOperationException("Order does not exist");

        // State machine automatically validates if we can modify pending orders
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Cannot add items to order with status {Status}");

        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("Product name is required", nameof(productName));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative", nameof(unitPrice));

        RaiseEvent(new OrderItemAddedEvent(productName, quantity, unitPrice));
    }

    public void Ship(string shippingAddress, string trackingNumber)
    {
        if (Id == Guid.Empty)
            throw new InvalidOperationException("Order does not exist");

        // Use state machine to validate transition
        if (!_stateMachine.CanTransitionTo(OrderStatus.Shipped))
            throw new InvalidOperationException(
                $"Cannot ship order. Current status: {Status}. " +
                $"Allowed transitions: {string.Join(", ", _stateMachine.GetAllowedTransitions())}");

        if (Items.Count == 0)
            throw new InvalidOperationException("Cannot ship order with no items");

        if (string.IsNullOrWhiteSpace(shippingAddress))
            throw new ArgumentException("Shipping address is required", nameof(shippingAddress));

        if (string.IsNullOrWhiteSpace(trackingNumber))
            throw new ArgumentException("Tracking number is required", nameof(trackingNumber));

        RaiseEvent(new OrderShippedEvent(shippingAddress, trackingNumber));
    }

    public void Cancel(string reason)
    {
        if (Id == Guid.Empty)
            throw new InvalidOperationException("Order does not exist");

        // Use state machine to validate transition
        if (!_stateMachine.CanTransitionTo(OrderStatus.Cancelled))
            throw new InvalidOperationException(
                $"Cannot cancel order. Current status: {Status}. " +
                $"Allowed transitions: {string.Join(", ", _stateMachine.GetAllowedTransitions())}");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required", nameof(reason));

        RaiseEvent(new OrderCancelledEvent(reason));
    }

    /// <summary>
    /// Gets the list of allowed next states from current state.
    /// Useful for UI to show available actions.
    /// </summary>
    public IEnumerable<OrderStatus> GetAllowedNextStates()
    {
        return _stateMachine.GetAllowedTransitions();
    }

    // Event Handlers - Apply state changes

    private void Apply(OrderCreatedEvent @event)
    {
        Id = @event.OrderId;
        CustomerId = @event.CustomerId;
        Total = @event.Total;

        // Set initial state (use SetState to avoid validation during replay)
        _stateMachine.SetState(OrderStatus.Pending);
    }

    private void Apply(OrderItemAddedEvent @event)
    {
        Items.Add(new OrderItem
        {
            ProductName = @event.ProductName,
            Quantity = @event.Quantity,
            UnitPrice = @event.UnitPrice
        });

        Total += @event.Quantity * @event.UnitPrice;
    }

    private void Apply(OrderShippedEvent @event)
    {
        ShippingAddress = @event.ShippingAddress;
        TrackingNumber = @event.TrackingNumber;

        // Transition state (use SetState during replay to avoid validation)
        _stateMachine.SetState(OrderStatus.Shipped);
    }

    private void Apply(OrderCancelledEvent @event)
    {
        CancellationReason = @event.Reason;

        // Transition state
        _stateMachine.SetState(OrderStatus.Cancelled);
    }
}
