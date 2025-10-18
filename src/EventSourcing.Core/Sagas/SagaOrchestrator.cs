using EventSourcing.Abstractions.Sagas;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Core.Sagas;

/// <summary>
/// Orchestrates saga execution with automatic compensation on failure
/// </summary>
public class SagaOrchestrator : ISagaOrchestrator
{
    private readonly ISagaStore _sagaStore;
    private readonly ILogger<SagaOrchestrator> _logger;

    public SagaOrchestrator(ISagaStore sagaStore, ILogger<SagaOrchestrator> logger)
    {
        _sagaStore = sagaStore ?? throw new ArgumentNullException(nameof(sagaStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ISaga<TData>> ExecuteAsync<TData>(ISaga<TData> saga, CancellationToken cancellationToken = default)
        where TData : class
    {
        if (saga == null) throw new ArgumentNullException(nameof(saga));

        _logger.LogInformation("Starting saga {SagaName} with ID {SagaId}", saga.SagaName, saga.SagaId);

        if (saga is not Saga<TData> mutableSaga)
        {
            throw new InvalidOperationException("Saga must be of type Saga<TData>");
        }

        try
        {
            mutableSaga.Status = SagaStatus.Running;
            await _sagaStore.SaveAsync(saga, cancellationToken);

            // Execute all steps in order
            for (int i = 0; i < saga.Steps.Count; i++)
            {
                var step = saga.Steps[i];
                mutableSaga.CurrentStepIndex = i;
                await _sagaStore.SaveAsync(saga, cancellationToken);

                _logger.LogInformation("Executing step {StepIndex}/{TotalSteps}: {StepName} for saga {SagaId}",
                    i + 1, saga.Steps.Count, step.Name, saga.SagaId);

                var success = await step.ExecuteAsync(saga.Data, cancellationToken);

                if (!success)
                {
                    _logger.LogWarning("Step {StepName} failed for saga {SagaId}. Starting compensation...",
                        step.Name, saga.SagaId);

                    await CompensateAsync(mutableSaga, i, cancellationToken);
                    return saga;
                }

                _logger.LogInformation("Step {StepName} completed successfully for saga {SagaId}",
                    step.Name, saga.SagaId);
            }

            // All steps completed successfully
            mutableSaga.Status = SagaStatus.Completed;
            await _sagaStore.SaveAsync(saga, cancellationToken);

            _logger.LogInformation("Saga {SagaName} with ID {SagaId} completed successfully",
                saga.SagaName, saga.SagaId);

            return saga;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during saga {SagaId} execution. Starting compensation...",
                saga.SagaId);

            await CompensateAsync(mutableSaga, mutableSaga.CurrentStepIndex, cancellationToken);
            throw;
        }
    }

    private async Task CompensateAsync<TData>(Saga<TData> saga, int failedStepIndex, CancellationToken cancellationToken)
        where TData : class
    {
        saga.Status = SagaStatus.Compensating;
        await _sagaStore.SaveAsync(saga, cancellationToken);

        _logger.LogInformation("Starting compensation for saga {SagaId} from step {StepIndex}",
            saga.SagaId, failedStepIndex);

        // Compensate all completed steps in reverse order
        for (int i = failedStepIndex - 1; i >= 0; i--)
        {
            var step = saga.Steps[i];

            _logger.LogInformation("Compensating step {StepIndex}: {StepName} for saga {SagaId}",
                i + 1, step.Name, saga.SagaId);

            try
            {
                var success = await step.CompensateAsync(saga.Data, cancellationToken);

                if (!success)
                {
                    _logger.LogError("Compensation failed for step {StepName} in saga {SagaId}",
                        step.Name, saga.SagaId);

                    saga.Status = SagaStatus.CompensationFailed;
                    await _sagaStore.SaveAsync(saga, cancellationToken);
                    return;
                }

                _logger.LogInformation("Step {StepName} compensated successfully for saga {SagaId}",
                    step.Name, saga.SagaId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during compensation of step {StepName} in saga {SagaId}",
                    step.Name, saga.SagaId);

                saga.Status = SagaStatus.CompensationFailed;
                await _sagaStore.SaveAsync(saga, cancellationToken);
                throw;
            }
        }

        saga.Status = SagaStatus.Compensated;
        await _sagaStore.SaveAsync(saga, cancellationToken);

        _logger.LogInformation("Saga {SagaId} compensated successfully", saga.SagaId);
    }

    public async Task<ISaga<TData>?> GetSagaAsync<TData>(string sagaId, CancellationToken cancellationToken = default)
        where TData : class
    {
        return await _sagaStore.LoadAsync<TData>(sagaId, cancellationToken);
    }
}
