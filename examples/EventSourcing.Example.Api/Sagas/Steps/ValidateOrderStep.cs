using EventSourcing.Core.Sagas;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Example.Api.Sagas.Steps;

/// <summary>
/// Validates the order data before processing
/// </summary>
public class ValidateOrderStep : SagaStepBase<OrderData>
{
    private readonly ILogger<ValidateOrderStep> _logger;

    public ValidateOrderStep(ILogger<ValidateOrderStep> logger)
    {
        _logger = logger;
    }

    public override string Name => "ValidateOrder";

    public override Task<bool> ExecuteAsync(OrderData data, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating order {OrderId}", data.OrderId);

        // Validate order data
        if (data.Items == null || !data.Items.Any())
        {
            _logger.LogWarning("Order {OrderId} has no items", data.OrderId);
            return Task.FromResult(false);
        }

        if (data.TotalAmount <= 0)
        {
            _logger.LogWarning("Order {OrderId} has invalid total amount: {Amount}", data.OrderId, data.TotalAmount);
            return Task.FromResult(false);
        }

        _logger.LogInformation("Order {OrderId} validated successfully", data.OrderId);
        return Task.FromResult(true);
    }

    public override Task<bool> CompensateAsync(OrderData data, CancellationToken cancellationToken = default)
    {
        // Nothing to compensate for validation
        _logger.LogInformation("No compensation needed for validation step");
        return Task.FromResult(true);
    }
}
