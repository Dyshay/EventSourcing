using EventSourcing.Core.Sagas;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Example.Api.Sagas.Steps;

/// <summary>
/// Reserves inventory for the order items
/// </summary>
public class ReserveInventoryStep : SagaStepBase<OrderData>
{
    private readonly ILogger<ReserveInventoryStep> _logger;

    public ReserveInventoryStep(ILogger<ReserveInventoryStep> logger)
    {
        _logger = logger;
    }

    public override string Name => "ReserveInventory";

    public override Task<bool> ExecuteAsync(OrderData data, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reserving inventory for order {OrderId}", data.OrderId);

        // Simulate inventory reservation
        // In a real system, this would call an inventory service
        data.ReservationId = Guid.NewGuid().ToString();

        _logger.LogInformation("Inventory reserved for order {OrderId} with reservation ID {ReservationId}",
            data.OrderId, data.ReservationId);

        return Task.FromResult(true);
    }

    public override Task<bool> CompensateAsync(OrderData data, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Releasing inventory reservation {ReservationId} for order {OrderId}",
            data.ReservationId, data.OrderId);

        // Simulate releasing the reservation
        // In a real system, this would call an inventory service to release the reservation
        data.ReservationId = null;

        _logger.LogInformation("Inventory reservation released for order {OrderId}", data.OrderId);
        return Task.FromResult(true);
    }
}
