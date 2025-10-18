using EventSourcing.Abstractions.Sagas;
using EventSourcing.Core.Sagas;
using EventSourcing.Example.Api.Sagas;
using EventSourcing.Example.Api.Sagas.Steps;
using Microsoft.AspNetCore.Mvc;

namespace EventSourcing.Example.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SagaController : ControllerBase
{
    private readonly ISagaOrchestrator _sagaOrchestrator;
    private readonly ILogger<SagaController> _logger;

    public SagaController(ISagaOrchestrator sagaOrchestrator, ILogger<SagaController> logger)
    {
        _sagaOrchestrator = sagaOrchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Creates and processes an order using the saga pattern
    /// </summary>
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        [FromServices] ILogger<ValidateOrderStep> validateLogger,
        [FromServices] ILogger<ReserveInventoryStep> reserveLogger,
        [FromServices] ILogger<ProcessPaymentStep> paymentLogger,
        [FromServices] ILogger<ConfirmOrderStep> confirmLogger,
        CancellationToken cancellationToken)
    {
        try
        {
            var orderData = new OrderData
            {
                OrderId = Guid.NewGuid(),
                CustomerId = request.CustomerId,
                Items = request.Items,
                TotalAmount = request.Items.Sum(i => i.Price * i.Quantity)
            };

            // Create the saga with all steps
            var saga = new Saga<OrderData>("OrderProcessing", orderData)
                .AddSteps(
                    new ValidateOrderStep(validateLogger),
                    new ReserveInventoryStep(reserveLogger),
                    new ProcessPaymentStep(paymentLogger),
                    new ConfirmOrderStep(confirmLogger)
                );

            // Execute the saga
            _logger.LogInformation("Starting order saga for order {OrderId}", orderData.OrderId);
            var result = await _sagaOrchestrator.ExecuteAsync(saga, cancellationToken);

            if (result.Status == SagaStatus.Completed)
            {
                return Ok(new
                {
                    orderId = orderData.OrderId,
                    status = "completed",
                    paymentTransactionId = orderData.PaymentTransactionId,
                    reservationId = orderData.ReservationId
                });
            }
            else if (result.Status == SagaStatus.Compensated)
            {
                return BadRequest(new
                {
                    orderId = orderData.OrderId,
                    status = "failed",
                    message = "Order processing failed and was rolled back successfully"
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    orderId = orderData.OrderId,
                    status = result.Status.ToString(),
                    message = "Order processing failed"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order saga");
            return StatusCode(500, new { message = "An error occurred while processing the order" });
        }
    }

    /// <summary>
    /// Gets the status of a saga by its ID
    /// </summary>
    [HttpGet("{sagaId}")]
    public async Task<IActionResult> GetSagaStatus(string sagaId, CancellationToken cancellationToken)
    {
        try
        {
            var saga = await _sagaOrchestrator.GetSagaAsync<OrderData>(sagaId, cancellationToken);

            if (saga == null)
            {
                return NotFound(new { message = "Saga not found" });
            }

            return Ok(new
            {
                sagaId = saga.SagaId,
                sagaName = saga.SagaName,
                status = saga.Status.ToString(),
                currentStep = saga.CurrentStepIndex >= 0 && saga.CurrentStepIndex < saga.Steps.Count
                    ? saga.Steps[saga.CurrentStepIndex].Name
                    : "N/A",
                data = saga.Data
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving saga status");
            return StatusCode(500, new { message = "An error occurred while retrieving saga status" });
        }
    }
}

public record CreateOrderRequest(
    Guid CustomerId,
    List<OrderItem> Items
);
