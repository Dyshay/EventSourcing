using EventSourcing.Abstractions;
using EventSourcing.CQRS.Commands;

namespace EventSourcing.CQRS.Context;

/// <summary>
/// Execution context for a command, providing audit trail and traceability.
/// Tracks the command execution lifecycle and all generated events.
/// Supports object pooling for better performance.
/// </summary>
public class CommandContext
{
    /// <summary>
    /// Unique identifier for this command execution
    /// </summary>
    public Guid CommandId { get; set; }

    /// <summary>
    /// The type name of the command being executed
    /// </summary>
    public string CommandType { get; set; } = string.Empty;

    /// <summary>
    /// When the command execution started
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// When the command execution completed (null if still running)
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// The user or service that initiated the command
    /// </summary>
    public string? InitiatedBy { get; set; }

    /// <summary>
    /// Correlation ID for distributed tracing
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Additional metadata about the command execution
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Events generated during command execution (for audit trail)
    /// </summary>
    public List<IEvent> GeneratedEvents { get; } = new();

    /// <summary>
    /// Whether the command execution was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the command failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The aggregate ID affected by this command
    /// </summary>
    public object? AggregateId { get; set; }

    /// <summary>
    /// Version of the aggregate after command execution
    /// </summary>
    public int? AggregateVersion { get; set; }

    /// <summary>
    /// Execution duration in milliseconds
    /// </summary>
    public long ExecutionTimeMs => CompletedAt.HasValue
        ? (long)(CompletedAt.Value - StartedAt).TotalMilliseconds
        : 0;

    /// <summary>
    /// Default constructor for object pooling
    /// </summary>
    public CommandContext()
    {
    }

    /// <summary>
    /// Constructor with command initialization (backward compatibility)
    /// </summary>
    public CommandContext(ICommand command, string? initiatedBy = null, string? correlationId = null)
    {
        Initialize(command, initiatedBy, correlationId);
    }

    /// <summary>
    /// Initialize the context with command data (used for object pooling)
    /// </summary>
    public void Initialize(ICommand command, string? initiatedBy = null, string? correlationId = null)
    {
        CommandId = command.CommandId;
        CommandType = command.GetType().Name;
        StartedAt = DateTimeOffset.UtcNow;
        InitiatedBy = initiatedBy;
        CorrelationId = correlationId ?? Guid.NewGuid().ToString();
        Metadata = command.Metadata ?? new Dictionary<string, object>();
        GeneratedEvents.Clear();
        Success = true;
        CompletedAt = null;
        ErrorMessage = null;
        AggregateId = null;
        AggregateVersion = null;
    }

    /// <summary>
    /// Reset the context to default state (used for object pooling)
    /// </summary>
    public void Reset()
    {
        CommandId = Guid.Empty;
        CommandType = string.Empty;
        StartedAt = default;
        CompletedAt = null;
        InitiatedBy = null;
        CorrelationId = null;
        Metadata.Clear();
        GeneratedEvents.Clear();
        Success = false;
        ErrorMessage = null;
        AggregateId = null;
        AggregateVersion = null;
    }

    /// <summary>
    /// Records an event generated during command execution
    /// </summary>
    public void RecordEvent(IEvent @event)
    {
        GeneratedEvents.Add(@event);
    }

    /// <summary>
    /// Records multiple events generated during command execution
    /// </summary>
    public void RecordEvents(IEnumerable<IEvent> events)
    {
        GeneratedEvents.AddRange(events);
    }

    /// <summary>
    /// Marks the command as completed successfully
    /// </summary>
    public void MarkSuccess(object? aggregateId = null, int? version = null)
    {
        CompletedAt = DateTimeOffset.UtcNow;
        Success = true;
        AggregateId = aggregateId;
        AggregateVersion = version;
    }

    /// <summary>
    /// Marks the command as failed
    /// </summary>
    public void MarkFailure(string errorMessage)
    {
        CompletedAt = DateTimeOffset.UtcNow;
        Success = false;
        ErrorMessage = errorMessage;
    }
}
