namespace EventSourcing.Abstractions;

/// <summary>
/// Base marker interface for all domain events.
/// Events represent immutable facts about state changes in the domain.
/// </summary>
public interface IEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Type name of the event for serialization/deserialization.
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Kind/category of the event (e.g., "user.created", "order.placed").
    /// Used for filtering, routing, and organizing events by category.
    /// </summary>
    string Kind { get; }
}
