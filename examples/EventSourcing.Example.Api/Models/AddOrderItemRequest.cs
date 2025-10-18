namespace EventSourcing.Example.Api.Models;

public record AddOrderItemRequest(
    string ProductName,
    int Quantity,
    decimal UnitPrice
);
