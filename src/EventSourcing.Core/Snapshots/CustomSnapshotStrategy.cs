using EventSourcing.Abstractions;

namespace EventSourcing.Core.Snapshots;

/// <summary>
/// Snapshot strategy that uses a custom predicate to determine when to create snapshots.
/// Provides maximum flexibility for complex snapshot logic.
/// </summary>
public class CustomSnapshotStrategy : ISnapshotStrategy
{
    private readonly Func<object, int, DateTimeOffset?, bool> _predicate;

    /// <summary>
    /// Creates a new custom snapshot strategy.
    /// </summary>
    /// <param name="predicate">
    /// Custom predicate that receives:
    /// - aggregate: The aggregate instance
    /// - eventCount Since LastSnapshot: Number of events since last snapshot
    /// - lastSnapshotTimestamp: Timestamp of last snapshot (null if none exists)
    /// Returns true if a snapshot should be created.
    /// </param>
    public CustomSnapshotStrategy(Func<object, int, DateTimeOffset?, bool> predicate)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public bool ShouldCreateSnapshot<TId>(
        IAggregate<TId> aggregate,
        int eventCountSinceLastSnapshot,
        DateTimeOffset? lastSnapshotTimestamp) where TId : notnull
    {
        return _predicate(aggregate, eventCountSinceLastSnapshot, lastSnapshotTimestamp);
    }
}
