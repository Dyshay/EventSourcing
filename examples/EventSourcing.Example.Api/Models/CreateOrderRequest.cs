namespace EventSourcing.Example.Api.Models;

public record CreateOrderRequest(
    Guid CustomerId
);
