namespace EventSourcing.Example.Api.Application.DTOs;

public record OrderDto
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public string? ShippingAddress { get; init; }
    public string Status { get; init; } = string.Empty;
    public List<OrderItemDto> Items { get; init; } = new();
    public decimal Total { get; init; }
    public string? TrackingNumber { get; init; }
}

public record OrderItemDto
{
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}

public record OrderStatusDto
{
    public Guid OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? TrackingNumber { get; init; }
}

public record OrderActionsDto
{
    public Guid OrderId { get; init; }
    public List<string> AllowedActions { get; init; } = new();
}
