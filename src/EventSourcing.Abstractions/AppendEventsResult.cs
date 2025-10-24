namespace EventSourcing.Abstractions;

/// <summary>
/// Result of appending events operation containing the IDs of inserted events.
/// </summary>
public class AppendEventsResult
{
    /// <summary>
    /// List of IDs of events that were successfully inserted.
    /// </summary>
    public IReadOnlyList<Guid> EventIds { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// Number of events inserted.
    /// </summary>
    public int Count => EventIds.Count;

    /// <summary>
    /// The new version of the aggregate after inserting the events.
    /// </summary>
    public int NewVersion { get; init; }

    /// <summary>
    /// Constructor to create an append events result.
    /// </summary>
    /// <param name="eventIds">IDs of inserted events</param>
    /// <param name="newVersion">New version of the aggregate</param>
    public AppendEventsResult(IEnumerable<Guid> eventIds, int newVersion)
    {
        EventIds = eventIds.ToList().AsReadOnly();
        NewVersion = newVersion;
    }

    /// <summary>
    /// Create an empty result (no events inserted).
    /// </summary>
    public static AppendEventsResult Empty(int currentVersion) =>
        new(Array.Empty<Guid>(), currentVersion);
}