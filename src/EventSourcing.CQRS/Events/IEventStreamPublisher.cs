using EventSourcing.Abstractions;

namespace EventSourcing.CQRS.Events;

/// <summary>
/// Publisher for sending events to external streaming systems (Kafka, RabbitMQ, Azure Event Hubs, etc.)
/// </summary>
public interface IEventStreamPublisher
{
    /// <summary>
    /// Publishes an event to the external stream
    /// </summary>
    Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple events to the external stream in a batch
    /// </summary>
    Task PublishBatchAsync(IEnumerable<IEvent> events, CancellationToken cancellationToken = default);
}
