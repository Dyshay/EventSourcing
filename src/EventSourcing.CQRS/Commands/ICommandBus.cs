using EventSourcing.Abstractions;

namespace EventSourcing.CQRS.Commands;

/// <summary>
/// Central bus for dispatching commands to their handlers.
/// Provides a pipeline for middleware processing (validation, logging, etc.)
/// </summary>
public interface ICommandBus
{
    /// <summary>
    /// Sends a command that generates a specific event type
    /// </summary>
    /// <typeparam name="TEvent">The event type that will be generated</typeparam>
    /// <param name="command">The command to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Command result containing the generated event</returns>
    Task<CommandResult<TEvent>> SendAsync<TEvent>(
        ICommand<TEvent> command,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    /// <summary>
    /// Sends a command that generates multiple events
    /// </summary>
    /// <param name="command">The command to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Command result containing all generated events</returns>
    Task<CommandResult<IEnumerable<IEvent>>> SendAsync(
        ICommandMultiEvent command,
        CancellationToken cancellationToken = default);
}
