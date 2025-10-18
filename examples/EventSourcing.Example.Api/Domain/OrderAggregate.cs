using EventSourcing.Core;
using EventSourcing.Example.Api.Domain.Events;

namespace EventSourcing.Example.Api.Domain;

public class OrderAggregate : AggregateBase<Guid>
{
    public override Guid Id { get; protected set; }
    public Guid CustomerId { get; protected set; }
    public decimal Total { get; protected set; }
    public List<OrderItem> Items { get; protected set; } = new();
    public OrderStatus Status { get; protected set; }
    public string? ShippingAddress { get; protected set; }
    public string? TrackingNumber { get; protected set; }
    public string? CancellationReason { get; protected set; }

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

        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Cannot ship order with status {Status}");

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

        if (Status == OrderStatus.Shipped)
            throw new InvalidOperationException("Cannot cancel shipped order");

        if (Status == OrderStatus.Cancelled)
            return; // Already cancelled

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required", nameof(reason));

        RaiseEvent(new OrderCancelledEvent(reason));
    }

    // Event Handlers - Apply state changes

    private void Apply(OrderCreatedEvent @event)
    {
        Id = @event.OrderId;
        CustomerId = @event.CustomerId;
        Total = @event.Total;
        Status = OrderStatus.Pending;
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
        Status = OrderStatus.Shipped;
    }

    private void Apply(OrderCancelledEvent @event)
    {
        CancellationReason = @event.Reason;
        Status = OrderStatus.Cancelled;
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
