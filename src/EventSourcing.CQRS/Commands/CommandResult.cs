using EventSourcing.Abstractions;

namespace EventSourcing.CQRS.Commands;

/// <summary>
/// Represents the result of executing a command.
/// Contains the generated event(s) and execution metadata.
/// </summary>
/// <typeparam name="TData">The type of data returned (event or events)</typeparam>
public class CommandResult<TData>
{
    /// <summary>
    /// The data returned from command execution (event or collection of events)
    /// </summary>
    public TData Data { get; }

    /// <summary>
    /// The aggregate ID affected by this command (if applicable)
    /// </summary>
    public object? AggregateId { get; init; }

    /// <summary>
    /// The version of the aggregate after the command was executed
    /// </summary>
    public int? Version { get; init; }

    /// <summary>
    /// Whether the command execution was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the command failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The command ID that generated this result
    /// </summary>
    public Guid CommandId { get; init; }

    /// <summary>
    /// Timestamp when the command was executed
    /// </summary>
    public DateTimeOffset ExecutedAt { get; init; }

    /// <summary>
    /// Execution duration in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; init; }

    public CommandResult(TData data)
    {
        Data = data;
        Success = true;
        ExecutedAt = DateTimeOffset.UtcNow;
    }

    public CommandResult(string errorMessage)
    {
        Data = default!;
        Success = false;
        ErrorMessage = errorMessage;
        ExecutedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Creates a successful command result
    /// </summary>
    public static CommandResult<TData> SuccessResult(TData data, object? aggregateId = null, int? version = null)
    {
        return new CommandResult<TData>(data)
        {
            AggregateId = aggregateId,
            Version = version,
            Success = true
        };
    }

    /// <summary>
    /// Creates a failed command result
    /// </summary>
    public static CommandResult<TData> FailureResult(string errorMessage)
    {
        return new CommandResult<TData>(errorMessage)
        {
            Success = false
        };
    }
}
