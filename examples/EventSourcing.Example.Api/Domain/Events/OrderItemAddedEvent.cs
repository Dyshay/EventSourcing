using EventSourcing.Core;

namespace EventSourcing.Example.Api.Domain.Events;

public class OrderItemAddedEvent : DomainEvent
{
    public string ProductName { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }

    public OrderItemAddedEvent(string productName, int quantity, decimal unitPrice)
    {
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
