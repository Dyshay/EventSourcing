using MediatR;

namespace EventSourcing.Core.CQRS;

/// <summary>
/// Base record for commands that modify aggregate state.
/// Commands are imperative - they express intent to change state.
/// </summary>
public abstract record Command : IRequest
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Base record for commands that return a result.
/// </summary>
/// <typeparam name="TResult">The type of result returned</typeparam>
public abstract record Command<TResult> : IRequest<TResult>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Standard result for command handlers that return an aggregate ID.
/// </summary>
/// <param name="AggregateId">The ID of the created or modified aggregate</param>
/// <param name="Version">The new version of the aggregate</param>
public record CommandResult(
    string AggregateId,
    int Version
);

/// <summary>
/// Result indicating command execution status with optional error.
/// </summary>
/// <param name="Success">Whether the command succeeded</param>
/// <param name="AggregateId">The aggregate ID (if applicable)</param>
/// <param name="Version">The new version (if applicable)</param>
/// <param name="ErrorMessage">Error message if failed</param>
public record CommandExecutionResult(
    bool Success,
    string? AggregateId = null,
    int? Version = null,
    string? ErrorMessage = null
)
{
    public static CommandExecutionResult Succeeded(string aggregateId, int version) =>
        new(true, aggregateId, version);

    public static CommandExecutionResult Failed(string errorMessage) =>
        new(false, ErrorMessage: errorMessage);
}
