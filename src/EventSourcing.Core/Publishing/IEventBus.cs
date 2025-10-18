using EventSourcing.Abstractions;

namespace EventSourcing.Core.Publishing;

/// <summary>
/// Event bus for dispatching events to projections and external publishers.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all registered handlers (projections and publishers).
    /// </summary>
    /// <param name="event">The event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple events to all registered handlers.
    /// </summary>
    /// <param name="events">The events to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task PublishAsync(IEnumerable<IEvent> events, CancellationToken cancellationToken = default);
}
