namespace EventSourcing.Abstractions;

/// <summary>
/// Repository for managing aggregate lifecycle (loading and saving).
/// Orchestrates event store and snapshot store to provide efficient aggregate hydration.
/// </summary>
/// <typeparam name="TAggregate">Type of the aggregate</typeparam>
/// <typeparam name="TId">Type of the aggregate identifier</typeparam>
public interface IAggregateRepository<TAggregate, TId>
    where TAggregate : IAggregate<TId>
    where TId : notnull
{
    /// <summary>
    /// Loads an aggregate by its identifier.
    /// Uses snapshots when available and replays subsequent events.
    /// </summary>
    /// <param name="aggregateId">Identifier of the aggregate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The hydrated aggregate</returns>
    /// <exception cref="AggregateNotFoundException">Thrown when aggregate doesn't exist</exception>
    Task<TAggregate> GetByIdAsync(TId aggregateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an aggregate by persisting its uncommitted events.
    /// May create a snapshot based on configured snapshot strategy.
    /// </summary>
    /// <param name="aggregate">The aggregate to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    /// <exception cref="ConcurrencyException">Thrown when a concurrency conflict is detected</exception>
    Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an aggregate with the given identifier exists.
    /// </summary>
    /// <param name="aggregateId">Identifier of the aggregate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the aggregate exists, false otherwise</returns>
    Task<bool> ExistsAsync(TId aggregateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an aggregate by persisting its uncommitted events and returns the result with event IDs.
    /// May create a snapshot based on configured snapshot strategy.
    /// </summary>
    /// <param name="aggregate">The aggregate to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the IDs of inserted events and new version</returns>
    /// <exception cref="ConcurrencyException">Thrown when a concurrency conflict is detected</exception>
    Task<AppendEventsResult> SaveWithResultAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a single event to the aggregate without using uncommitted events and returns the event ID.
    /// Updates the aggregate version and may create a snapshot based on configured strategy.
    /// </summary>
    /// <param name="aggregateId">Identifier of the aggregate</param>
    /// <param name="event">Event to append</param>
    /// <param name="expectedVersion">Expected current version for optimistic concurrency</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the inserted event</returns>
    /// <exception cref="ConcurrencyException">Thrown when expected version doesn't match</exception>
    Task<Guid> AppendEventAsync(TId aggregateId, IEvent @event, int expectedVersion, CancellationToken cancellationToken = default);
}

/// <summary>
/// Exception thrown when an aggregate cannot be found.
/// </summary>
public class AggregateNotFoundException : Exception
{
    public AggregateNotFoundException(string message) : base(message)
    {
    }

    public AggregateNotFoundException(object aggregateId, Type aggregateType)
        : base($"Aggregate '{aggregateType.Name}' with Id '{aggregateId}' was not found.")
    {
        AggregateId = aggregateId;
        AggregateType = aggregateType;
    }

    public object? AggregateId { get; }
    public Type? AggregateType { get; }
}
