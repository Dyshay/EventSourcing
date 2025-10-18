namespace EventSourcing.Abstractions;

/// <summary>
/// Represents an aggregate root in the event-sourced domain model.
/// Aggregates maintain consistency boundaries and are reconstructed from events.
/// </summary>
/// <typeparam name="TId">Type of the aggregate identifier</typeparam>
public interface IAggregate<TId> where TId : notnull
{
    /// <summary>
    /// Unique identifier for this aggregate instance.
    /// </summary>
    TId Id { get; }

    /// <summary>
    /// Current version of the aggregate (number of events applied).
    /// Used for optimistic concurrency control.
    /// </summary>
    int Version { get; set; }

    /// <summary>
    /// Collection of uncommitted events raised by this aggregate.
    /// These events will be persisted when the aggregate is saved.
    /// </summary>
    IReadOnlyList<IEvent> UncommittedEvents { get; }

    /// <summary>
    /// Raises a new domain event for this aggregate.
    /// The event will be added to uncommitted events and applied to the aggregate state.
    /// </summary>
    /// <param name="event">The domain event to raise</param>
    void RaiseEvent(IEvent @event);

    /// <summary>
    /// Marks all uncommitted events as committed.
    /// Called after events are successfully persisted to the event store.
    /// </summary>
    void MarkEventsAsCommitted();

    /// <summary>
    /// Loads the aggregate state from historical events.
    /// </summary>
    /// <param name="events">Historical events to replay</param>
    void LoadFromHistory(IEnumerable<IEvent> events);
}
