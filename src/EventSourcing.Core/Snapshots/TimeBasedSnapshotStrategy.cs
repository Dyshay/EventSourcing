using EventSourcing.Abstractions;

namespace EventSourcing.Core.Snapshots;

/// <summary>
/// Snapshot strategy that creates snapshots based on time elapsed since the last snapshot.
/// Example: Create a snapshot every hour.
/// </summary>
public class TimeBasedSnapshotStrategy : ISnapshotStrategy
{
    private readonly TimeSpan _interval;

    /// <summary>
    /// Creates a new time-based snapshot strategy.
    /// </summary>
    /// <param name="interval">Time interval between snapshots</param>
    public TimeBasedSnapshotStrategy(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentException("Interval must be greater than zero", nameof(interval));
        }

        _interval = interval;
    }

    public bool ShouldCreateSnapshot<TId>(
        IAggregate<TId> aggregate,
        int eventCountSinceLastSnapshot,
        DateTimeOffset? lastSnapshotTimestamp) where TId : notnull
    {
        // If no snapshot exists yet, create one if there are uncommitted events
        if (lastSnapshotTimestamp == null)
        {
            return eventCountSinceLastSnapshot > 0;
        }

        var elapsed = DateTimeOffset.UtcNow - lastSnapshotTimestamp.Value;
        return elapsed >= _interval && eventCountSinceLastSnapshot > 0;
    }
}
