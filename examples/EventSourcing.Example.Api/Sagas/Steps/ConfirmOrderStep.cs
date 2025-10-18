using EventSourcing.Core.Sagas;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Example.Api.Sagas.Steps;

/// <summary>
/// Confirms and finalizes the order
/// </summary>
public class ConfirmOrderStep : SagaStepBase<OrderData>
{
    private readonly ILogger<ConfirmOrderStep> _logger;

    public ConfirmOrderStep(ILogger<ConfirmOrderStep> logger)
    {
        _logger = logger;
    }

    public override string Name => "ConfirmOrder";

    public override Task<bool> ExecuteAsync(OrderData data, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Confirming order {OrderId}", data.OrderId);

        // Simulate order confirmation
        // In a real system, this would update the order status and trigger fulfillment

        _logger.LogInformation("Order {OrderId} confirmed successfully. Ready for shipment.", data.OrderId);
        return Task.FromResult(true);
    }

    public override Task<bool> CompensateAsync(OrderData data, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Canceling order confirmation for order {OrderId}", data.OrderId);

        // Simulate order cancellation
        // In a real system, this would update the order status to cancelled

        _logger.LogInformation("Order {OrderId} cancelled", data.OrderId);
        return Task.FromResult(true);
    }
}
