using EventSourcing.Core.Snapshots;

namespace EventSourcing.Core.Configuration;

/// <summary>
/// Configuration options for event sourcing.
/// </summary>
public class EventSourcingOptions
{
    /// <summary>
    /// MongoDB connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// MongoDB database name.
    /// </summary>
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Snapshot strategy to use.
    /// Defaults to creating a snapshot every 10 events.
    /// </summary>
    public ISnapshotStrategy? SnapshotStrategy { get; set; }

    /// <summary>
    /// Whether to enable event publishing (projections and external publishers).
    /// Defaults to true.
    /// </summary>
    public bool EnableEventPublishing { get; set; } = true;
}
