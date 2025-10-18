using EventSourcing.Abstractions;

namespace EventSourcing.Core.Snapshots;

/// <summary>
/// Strategy interface for determining when to create snapshots.
/// </summary>
public interface ISnapshotStrategy
{
    /// <summary>
    /// Determines if a snapshot should be created for the given aggregate.
    /// </summary>
    /// <typeparam name="TId">Type of the aggregate identifier</typeparam>
    /// <param name="aggregate">The aggregate to potentially snapshot</param>
    /// <param name="eventCountSinceLastSnapshot">Number of events since the last snapshot</param>
    /// <param name="lastSnapshotTimestamp">Timestamp of the last snapshot, or null if no snapshot exists</param>
    /// <returns>True if a snapshot should be created, false otherwise</returns>
    bool ShouldCreateSnapshot<TId>(
        IAggregate<TId> aggregate,
        int eventCountSinceLastSnapshot,
        DateTimeOffset? lastSnapshotTimestamp) where TId : notnull;
}
