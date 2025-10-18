using EventSourcing.CQRS.Queries;

namespace EventSourcing.CQRS.Middleware;

/// <summary>
/// Delegate representing the next step in the query middleware pipeline
/// </summary>
public delegate Task<TResult> QueryHandlerDelegate<TResult>();

/// <summary>
/// Base interface for query middleware.
/// Middleware can intercept query execution for cross-cutting concerns.
/// </summary>
public interface IQueryMiddleware
{
    /// <summary>
    /// The order in which this middleware should execute (lower executes first)
    /// </summary>
    int Order { get; }
}

/// <summary>
/// Middleware for processing queries
/// </summary>
public interface IQueryMiddleware<TQuery, TResult> : IQueryMiddleware
    where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Invokes the middleware
    /// </summary>
    /// <param name="query">The query being executed</param>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<TResult> InvokeAsync(
        TQuery query,
        QueryHandlerDelegate<TResult> next,
        CancellationToken cancellationToken);
}
