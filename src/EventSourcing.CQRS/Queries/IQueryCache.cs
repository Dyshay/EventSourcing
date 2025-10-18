namespace EventSourcing.CQRS.Queries;

/// <summary>
/// Interface for query result caching
/// </summary>
public interface IQueryCache
{
    /// <summary>
    /// Gets a cached query result
    /// </summary>
    Task<(bool found, TResult? result)> GetAsync<TResult>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a query result in cache
    /// </summary>
    Task SetAsync<TResult>(string key, TResult result, CacheOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a cached query result
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all cached results for queries invalidated by the given event type
    /// </summary>
    Task InvalidateByEventAsync(string eventType, CancellationToken cancellationToken = default);
}
