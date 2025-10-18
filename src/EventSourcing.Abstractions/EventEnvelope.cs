namespace EventSourcing.Abstractions;

/// <summary>
/// Envelope containing event metadata and data separately to avoid duplication in API responses.
/// </summary>
public class EventEnvelope
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public required Guid EventId { get; init; }

    /// <summary>
    /// Type name of the event.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Kind/category of the event (e.g., "user.created", "order.placed").
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Event-specific data without metadata properties.
    /// Contains only the business properties specific to this event type.
    /// </summary>
    public required object Data { get; init; }
}
