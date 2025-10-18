using EventSourcing.Abstractions;

namespace EventSourcing.Core.Publishing;

/// <summary>
/// Interface for publishing events to external systems (message bus, queue, etc.).
/// Implement this interface to integrate with RabbitMQ, Azure Service Bus, Kafka, etc.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event to an external system.
    /// </summary>
    /// <param name="event">The event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default);
}
