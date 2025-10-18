using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public class OrderCreatedEvent : DomainEvent
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal Total { get; init; }

    public OrderCreatedEvent(Guid orderId, Guid customerId, decimal total)
    {
        OrderId = orderId;
        CustomerId = customerId;
        Total = total;
    }
}
