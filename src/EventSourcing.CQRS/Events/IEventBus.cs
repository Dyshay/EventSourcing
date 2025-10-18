using EventSourcing.Abstractions;

namespace EventSourcing.CQRS.Events;

/// <summary>
/// Enhanced event bus with support for both synchronous and streaming event publishing
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event synchronously to all registered handlers (projections, etc.)
    /// </summary>
    Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple events in a batch
    /// </summary>
    Task PublishBatchAsync(IEnumerable<IEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event asynchronously to external streaming systems (Kafka, RabbitMQ, etc.)
    /// </summary>
    Task PublishToStreamAsync(IEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple events to the stream in a batch
    /// </summary>
    Task PublishBatchToStreamAsync(IEnumerable<IEvent> events, CancellationToken cancellationToken = default);
}
