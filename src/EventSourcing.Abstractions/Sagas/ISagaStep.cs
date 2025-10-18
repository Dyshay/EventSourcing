namespace EventSourcing.Abstractions.Sagas;

/// <summary>
/// Represents a single step in a saga with its action and compensation
/// </summary>
/// <typeparam name="TData">The type of data passed between saga steps</typeparam>
public interface ISagaStep<TData> where TData : class
{
    /// <summary>
    /// Gets the name of this saga step
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the step action
    /// </summary>
    /// <param name="data">The saga data context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if step succeeded, false otherwise</returns>
    Task<bool> ExecuteAsync(TData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the compensation logic when the saga needs to be rolled back
    /// </summary>
    /// <param name="data">The saga data context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if compensation succeeded, false otherwise</returns>
    Task<bool> CompensateAsync(TData data, CancellationToken cancellationToken = default);
}
