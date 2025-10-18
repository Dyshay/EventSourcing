namespace EventSourcing.Abstractions.Sagas;

/// <summary>
/// Provides persistence for saga state
/// </summary>
public interface ISagaStore
{
    /// <summary>
    /// Saves the current state of a saga
    /// </summary>
    /// <typeparam name="TData">The type of saga data</typeparam>
    /// <param name="saga">The saga to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveAsync<TData>(ISaga<TData> saga, CancellationToken cancellationToken = default)
        where TData : class;

    /// <summary>
    /// Loads a saga by its ID
    /// </summary>
    /// <typeparam name="TData">The type of saga data</typeparam>
    /// <param name="sagaId">The saga identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The saga if found, null otherwise</returns>
    Task<ISaga<TData>?> LoadAsync<TData>(string sagaId, CancellationToken cancellationToken = default)
        where TData : class;

    /// <summary>
    /// Deletes a saga from storage
    /// </summary>
    /// <param name="sagaId">The saga identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(string sagaId, CancellationToken cancellationToken = default);
}
