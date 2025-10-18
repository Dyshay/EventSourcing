namespace EventSourcing.Abstractions;

/// <summary>
/// Abstraction for persisting and retrieving events in an append-only event store.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Appends a collection of events to the event store for a specific aggregate.
    /// </summary>
    /// <typeparam name="TId">Type of the aggregate identifier</typeparam>
    /// <param name="aggregateId">Identifier of the aggregate</param>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="events">Events to append</param>
    /// <param name="expectedVersion">Expected current version for optimistic concurrency</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    /// <exception cref="ConcurrencyException">Thrown when expected version doesn't match</exception>
    Task AppendEventsAsync<TId>(
        TId aggregateId,
        string aggregateType,
        IEnumerable<IEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken = default) where TId : notnull;

    /// <summary>
    /// Retrieves all events for a specific aggregate.
    /// </summary>
    /// <typeparam name="TId">Type of the aggregate identifier</typeparam>
    /// <param name="aggregateId">Identifier of the aggregate</param>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All events for the aggregate in chronological order</returns>
    Task<IEnumerable<IEvent>> GetEventsAsync<TId>(
        TId aggregateId,
        string aggregateType,
        CancellationToken cancellationToken = default) where TId : notnull;

    /// <summary>
    /// Retrieves events for a specific aggregate starting from a specific version.
    /// Useful for loading events after a snapshot.
    /// </summary>
    /// <typeparam name="TId">Type of the aggregate identifier</typeparam>
    /// <param name="aggregateId">Identifier of the aggregate</param>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="fromVersion">Version to start from (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Events from the specified version onwards</returns>
    Task<IEnumerable<IEvent>> GetEventsAsync<TId>(
        TId aggregateId,
        string aggregateType,
        int fromVersion,
        CancellationToken cancellationToken = default) where TId : notnull;

    /// <summary>
    /// Retrieves all events for a specific aggregate type.
    /// Useful for event replay, building projections, or audit trails.
    /// </summary>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All events for the aggregate type in chronological order</returns>
    Task<IEnumerable<IEvent>> GetAllEventsAsync(
        string aggregateType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all events for a specific aggregate type starting from a timestamp.
    /// Useful for incremental processing or catching up projections.
    /// </summary>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="fromTimestamp">Timestamp to start from (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All events from the specified timestamp onwards</returns>
    Task<IEnumerable<IEvent>> GetAllEventsAsync(
        string aggregateType,
        DateTimeOffset fromTimestamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all events of a specific kind/category.
    /// Useful for filtering events by category (e.g., "user.created", "order.placed").
    /// </summary>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="kind">Event kind/category to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All events of the specified kind</returns>
    Task<IEnumerable<IEvent>> GetEventsByKindAsync(
        string aggregateType,
        string kind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all events matching any of the specified kinds/categories.
    /// Useful for filtering multiple event categories at once.
    /// </summary>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="kinds">Event kinds/categories to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All events matching any of the specified kinds</returns>
    Task<IEnumerable<IEvent>> GetEventsByKindsAsync(
        string aggregateType,
        IEnumerable<string> kinds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all events for a specific aggregate as envelopes (metadata + data separated).
    /// Useful for API responses to avoid data duplication.
    /// </summary>
    /// <typeparam name="TId">Type of the aggregate identifier</typeparam>
    /// <param name="aggregateId">Identifier of the aggregate</param>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All events as envelopes with separated metadata and data</returns>
    Task<IEnumerable<EventEnvelope>> GetEventEnvelopesAsync<TId>(
        TId aggregateId,
        string aggregateType,
        CancellationToken cancellationToken = default) where TId : notnull;

    /// <summary>
    /// Retrieves all events for a specific aggregate type as envelopes.
    /// Useful for API responses to avoid data duplication.
    /// </summary>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All events as envelopes with separated metadata and data</returns>
    Task<IEnumerable<EventEnvelope>> GetAllEventEnvelopesAsync(
        string aggregateType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all events for a specific aggregate type starting from a timestamp as envelopes.
    /// Useful for API responses to avoid data duplication.
    /// </summary>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="fromTimestamp">Timestamp to start from (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All events from the specified timestamp onwards as envelopes</returns>
    Task<IEnumerable<EventEnvelope>> GetAllEventEnvelopesAsync(
        string aggregateType,
        DateTimeOffset fromTimestamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all events of a specific kind as envelopes.
    /// Useful for API responses to avoid data duplication.
    /// </summary>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="kind">Event kind/category to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All events of the specified kind as envelopes</returns>
    Task<IEnumerable<EventEnvelope>> GetEventEnvelopesByKindAsync(
        string aggregateType,
        string kind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all events matching any of the specified kinds as envelopes.
    /// Useful for API responses to avoid data duplication.
    /// </summary>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="kinds">Event kinds/categories to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All events matching any of the specified kinds as envelopes</returns>
    Task<IEnumerable<EventEnvelope>> GetEventEnvelopesByKindsAsync(
        string aggregateType,
        IEnumerable<string> kinds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all distinct aggregate IDs for a given aggregate type.
    /// Useful for listing all aggregates without a dedicated read model.
    /// </summary>
    /// <param name="aggregateType">Type name of the aggregate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All distinct aggregate IDs</returns>
    Task<IEnumerable<string>> GetAllAggregateIdsAsync(
        string aggregateType,
        CancellationToken cancellationToken = default);
}
