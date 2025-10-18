namespace EventSourcing.Example.Api.Sagas;

/// <summary>
/// Data context passed between saga steps for order processing
/// </summary>
public class OrderData
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string? PaymentTransactionId { get; set; }
    public string? ReservationId { get; set; }
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
