using EventSourcing.Abstractions;

namespace EventSourcing.Core.Snapshots;

/// <summary>
/// Snapshot strategy that creates snapshots based on event count frequency.
/// Example: Create a snapshot every 10 events.
/// </summary>
public class FrequencySnapshotStrategy : ISnapshotStrategy
{
    private readonly int _frequency;

    /// <summary>
    /// Creates a new frequency-based snapshot strategy.
    /// </summary>
    /// <param name="frequency">Number of events between snapshots</param>
    public FrequencySnapshotStrategy(int frequency)
    {
        if (frequency <= 0)
        {
            throw new ArgumentException("Frequency must be greater than zero", nameof(frequency));
        }

        _frequency = frequency;
    }

    public bool ShouldCreateSnapshot<TId>(
        IAggregate<TId> aggregate,
        int eventCountSinceLastSnapshot,
        DateTimeOffset? lastSnapshotTimestamp) where TId : notnull
    {
        return eventCountSinceLastSnapshot >= _frequency;
    }
}
