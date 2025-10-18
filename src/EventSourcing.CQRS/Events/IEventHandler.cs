using EventSourcing.Abstractions;

namespace EventSourcing.CQRS.Events;

/// <summary>
/// Handler for domain events.
/// Event handlers should be idempotent and handle events asynchronously.
/// </summary>
/// <typeparam name="TEvent">The event type to handle</typeparam>
public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    /// <summary>
    /// Handles the event
    /// </summary>
    /// <param name="event">The event to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
