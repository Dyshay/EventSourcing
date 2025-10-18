namespace EventSourcing.Abstractions;

/// <summary>
/// Abstraction for persisting and retrieving aggregate snapshots.
/// Snapshots are point-in-time state captures used to optimize aggregate reconstruction.
/// </summary>
public interface ISnapshotStore
{
    /// <summary>
    /// Saves a snapshot of an aggregate's state.
    /// </summary>
    /// <typeparam name="TId">Type of the aggregate identifier</typeparam>
    /// <typeparam name="TAggregate">Type of the aggregate</typeparam>
    /// <param name="aggregateId">Identifier of the aggregate</param>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="aggregate">The aggregate to snapshot</param>
    /// <param name="version">Current version of the aggregate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task SaveSnapshotAsync<TId, TAggregate>(
        TId aggregateId,
        string aggregateType,
        TAggregate aggregate,
        int version,
        CancellationToken cancellationToken = default)
        where TId : notnull
        where TAggregate : IAggregate<TId>;

    /// <summary>
    /// Retrieves the latest snapshot for an aggregate.
    /// </summary>
    /// <typeparam name="TId">Type of the aggregate identifier</typeparam>
    /// <typeparam name="TAggregate">Type of the aggregate</typeparam>
    /// <param name="aggregateId">Identifier of the aggregate</param>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Snapshot data containing the aggregate and its version, or null if no snapshot exists</returns>
    Task<Snapshot<TAggregate>?> GetLatestSnapshotAsync<TId, TAggregate>(
        TId aggregateId,
        string aggregateType,
        CancellationToken cancellationToken = default)
        where TId : notnull
        where TAggregate : IAggregate<TId>;
}

/// <summary>
/// Represents a snapshot of an aggregate's state at a specific version.
/// </summary>
/// <typeparam name="TAggregate">Type of the aggregate</typeparam>
public record Snapshot<TAggregate>(TAggregate Aggregate, int Version, DateTimeOffset Timestamp);
