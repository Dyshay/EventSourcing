using EventSourcing.Core.Sagas;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Example.Api.Sagas.Steps;

/// <summary>
/// Processes payment for the order
/// </summary>
public class ProcessPaymentStep : SagaStepBase<OrderData>
{
    private readonly ILogger<ProcessPaymentStep> _logger;

    public ProcessPaymentStep(ILogger<ProcessPaymentStep> logger)
    {
        _logger = logger;
    }

    public override string Name => "ProcessPayment";

    public override Task<bool> ExecuteAsync(OrderData data, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing payment for order {OrderId}, amount: {Amount}",
            data.OrderId, data.TotalAmount);

        // Simulate payment processing
        // In a real system, this would call a payment gateway
        data.PaymentTransactionId = $"TXN-{Guid.NewGuid()}";

        _logger.LogInformation("Payment processed for order {OrderId} with transaction ID {TransactionId}",
            data.OrderId, data.PaymentTransactionId);

        return Task.FromResult(true);
    }

    public override Task<bool> CompensateAsync(OrderData data, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refunding payment {TransactionId} for order {OrderId}",
            data.PaymentTransactionId, data.OrderId);

        // Simulate refund
        // In a real system, this would call a payment gateway to refund the transaction
        data.PaymentTransactionId = null;

        _logger.LogInformation("Payment refunded for order {OrderId}", data.OrderId);
        return Task.FromResult(true);
    }
}
