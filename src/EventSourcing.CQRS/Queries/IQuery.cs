namespace EventSourcing.CQRS.Queries;

/// <summary>
/// Base interface for queries that return data without modifying state.
/// Queries are read-only operations.
/// </summary>
/// <typeparam name="TResult">The type of data returned by the query</typeparam>
public interface IQuery<TResult>
{
    /// <summary>
    /// Unique identifier for this query instance
    /// </summary>
    Guid QueryId { get; }

    /// <summary>
    /// Timestamp when the query was created
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Optional metadata associated with the query
    /// </summary>
    Dictionary<string, object>? Metadata { get; }
}

/// <summary>
/// Interface for temporal queries that retrieve data as of a specific point in time.
/// Enables time-travel debugging and historical data analysis.
/// </summary>
/// <typeparam name="TResult">The type of data returned by the query</typeparam>
public interface ITemporalQuery<TResult> : IQuery<TResult>
{
    /// <summary>
    /// The point in time to query data from
    /// </summary>
    DateTimeOffset AsOfDate { get; }
}
