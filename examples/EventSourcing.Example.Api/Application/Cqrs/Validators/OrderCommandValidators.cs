using EventSourcing.CQRS.Middleware;
using EventSourcing.Example.Api.Application.Cqrs.Commands;

namespace EventSourcing.Example.Api.Application.Cqrs.Validators;

public class CreateOrderCqrsCommandValidator : ICommandValidator<CreateOrderCqrsCommand>
{
    public Task<IEnumerable<string>> ValidateAsync(
        CreateOrderCqrsCommand command,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (command.CustomerId == Guid.Empty)
            errors.Add("Customer ID is required");

        return Task.FromResult<IEnumerable<string>>(errors);
    }
}

public class AddOrderItemCqrsCommandValidator : ICommandValidator<AddOrderItemCqrsCommand>
{
    public Task<IEnumerable<string>> ValidateAsync(
        AddOrderItemCqrsCommand command,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (command.OrderId == Guid.Empty)
            errors.Add("Order ID is required");

        if (string.IsNullOrWhiteSpace(command.ProductName))
            errors.Add("Product name is required");

        if (command.Quantity <= 0)
            errors.Add("Quantity must be greater than zero");

        if (command.UnitPrice < 0)
            errors.Add("Unit price cannot be negative");

        return Task.FromResult<IEnumerable<string>>(errors);
    }
}

public class ShipOrderCqrsCommandValidator : ICommandValidator<ShipOrderCqrsCommand>
{
    public Task<IEnumerable<string>> ValidateAsync(
        ShipOrderCqrsCommand command,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (command.OrderId == Guid.Empty)
            errors.Add("Order ID is required");

        if (string.IsNullOrWhiteSpace(command.TrackingNumber))
            errors.Add("Tracking number is required");

        return Task.FromResult<IEnumerable<string>>(errors);
    }
}

public class CancelOrderCqrsCommandValidator : ICommandValidator<CancelOrderCqrsCommand>
{
    public Task<IEnumerable<string>> ValidateAsync(
        CancelOrderCqrsCommand command,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (command.OrderId == Guid.Empty)
            errors.Add("Order ID is required");

        if (string.IsNullOrWhiteSpace(command.Reason))
            errors.Add("Cancellation reason is required");

        return Task.FromResult<IEnumerable<string>>(errors);
    }
}
