namespace EventSourcing.Abstractions.Sagas;

/// <summary>
/// Orchestrates the execution of sagas, handling both forward and compensation logic
/// </summary>
public interface ISagaOrchestrator
{
    /// <summary>
    /// Executes a saga from start to finish
    /// </summary>
    /// <typeparam name="TData">The type of saga data</typeparam>
    /// <param name="saga">The saga to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The final saga with its execution result</returns>
    Task<ISaga<TData>> ExecuteAsync<TData>(ISaga<TData> saga, CancellationToken cancellationToken = default)
        where TData : class;

    /// <summary>
    /// Gets a saga by its ID
    /// </summary>
    /// <typeparam name="TData">The type of saga data</typeparam>
    /// <param name="sagaId">The saga identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The saga if found, null otherwise</returns>
    Task<ISaga<TData>?> GetSagaAsync<TData>(string sagaId, CancellationToken cancellationToken = default)
        where TData : class;
}
