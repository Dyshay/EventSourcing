using EventSourcing.Core;
using EventSourcing.Core.StateMachine;
using EventSourcing.Example.Api.Domain.Events;

namespace EventSourcing.Example.Api.Domain;

public class OrderAggregate : AggregateBase<Guid>
{
    private readonly StateMachineWithEvents<OrderStatus> _stateMachine;

    public override Guid Id { get; protected set; }
    public Guid CustomerId { get; protected set; }
    public decimal Total { get; protected set; }
    public List<OrderItem> Items { get; protected set; } = new();
    public OrderStatus Status => _stateMachine.CurrentState;
    public string? ShippingAddress { get; protected set; }
    public string? TrackingNumber { get; protected set; }
    public string? CancellationReason { get; protected set; }

    // Parameterless constructor required by IAggregateRepository (event replay)
    public OrderAggregate()
    {
        // State machine with domain event emission
        _stateMachine = new StateMachineWithEvents<OrderStatus>(
            initialState: OrderStatus.Pending,
            aggregateType: nameof(OrderAggregate),
            getAggregateId: () => Id.ToString(),
            onTransition: (stateTransitionEvent) =>
            {
                // Emit state transition as a domain event
                // This will be published via IEventBus to MediatR (infrastructure concern)
                RaiseEvent(stateTransitionEvent);
            }
        );

        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        // Define allowed transitions
        _stateMachine.Allow(OrderStatus.Pending, OrderStatus.Shipped, OrderStatus.Cancelled);

        // Note: Shipped and Cancelled are terminal states (no transitions out)
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

        // Business rule validation
        if (Items.Count == 0)
            throw new InvalidOperationException("Cannot ship order with no items");

        if (string.IsNullOrWhiteSpace(shippingAddress))
            throw new ArgumentException("Shipping address is required", nameof(shippingAddress));

        if (string.IsNullOrWhiteSpace(trackingNumber))
            throw new ArgumentException("Tracking number is required", nameof(trackingNumber));

        // Emit business event
        RaiseEvent(new OrderShippedEvent(shippingAddress, trackingNumber));

        // State machine validates transition Pending → Shipped
        // Emits StateTransitionEvent<OrderStatus> domain event
        // Throws InvalidStateTransitionException if not allowed
        _stateMachine.TransitionToWithEvent(OrderStatus.Shipped);
    }

    public void Cancel(string reason)
    {
        if (Id == Guid.Empty)
            throw new InvalidOperationException("Order does not exist");

        if (Status == OrderStatus.Cancelled)
            return; // Already cancelled (idempotent)

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required", nameof(reason));

        // Emit business event
        RaiseEvent(new OrderCancelledEvent(reason));

        // State machine validates transition Pending → Cancelled
        // Emits StateTransitionEvent<OrderStatus> domain event
        // Throws InvalidStateTransitionException if trying to cancel Shipped order
        _stateMachine.TransitionToWithEvent(OrderStatus.Cancelled);
    }

    // Event Handlers - Apply state changes (used during event replay)

    private void Apply(OrderCreatedEvent @event)
    {
        Id = @event.OrderId;
        CustomerId = @event.CustomerId;
        Total = @event.Total;

        // SetState instead of TransitionTo - no validation during replay
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

        // SetState - no validation during replay (we trust the event history)
        _stateMachine.SetState(OrderStatus.Shipped);
    }

    private void Apply(OrderCancelledEvent @event)
    {
        CancellationReason = @event.Reason;

        // SetState - no validation during replay
        _stateMachine.SetState(OrderStatus.Cancelled);
    }
}

public class OrderItem
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public enum OrderStatus
{
    Pending,
    Shipped,
    Cancelled
}
