using EventSourcing.Abstractions;

namespace EventSourcing.CQRS.Commands;

/// <summary>
/// Marker interface for commands that don't return a specific result.
/// Commands represent the intent to change system state.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Unique identifier for this command instance
    /// </summary>
    Guid CommandId { get; }

    /// <summary>
    /// Timestamp when the command was created
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Optional metadata associated with the command
    /// </summary>
    Dictionary<string, object>? Metadata { get; }
}

/// <summary>
/// Base interface for commands that generate a specific event type.
/// This allows the framework to strongly-type the relationship between commands and events.
/// </summary>
/// <typeparam name="TEvent">The primary event type this command will generate</typeparam>
public interface ICommand<TEvent> : ICommand where TEvent : IEvent
{
}

/// <summary>
/// Base interface for commands that generate multiple events.
/// Useful for complex operations that affect multiple aggregates or emit multiple events.
/// </summary>
public interface ICommandMultiEvent : ICommand
{
}
