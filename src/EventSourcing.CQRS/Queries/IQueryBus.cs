namespace EventSourcing.CQRS.Queries;

/// <summary>
/// Central bus for dispatching queries to their handlers.
/// Supports optional caching for improved performance.
/// </summary>
public interface IQueryBus
{
    /// <summary>
    /// Sends a query and returns the result
    /// </summary>
    /// <typeparam name="TResult">The type of data returned</typeparam>
    /// <param name="query">The query to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The query result</returns>
    Task<TResult> SendAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query with optional caching
    /// </summary>
    /// <typeparam name="TResult">The type of data returned</typeparam>
    /// <param name="query">The query to send</param>
    /// <param name="cacheOptions">Caching options (null to disable caching)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The query result (possibly cached)</returns>
    Task<TResult> SendAsync<TResult>(
        IQuery<TResult> query,
        CacheOptions? cacheOptions,
        CancellationToken cancellationToken = default);
}
