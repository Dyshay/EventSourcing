using EventSourcing.CQRS.Commands;
using EventSourcing.CQRS.Context;

namespace EventSourcing.CQRS.Middleware;

/// <summary>
/// Delegate representing the next step in the middleware pipeline
/// </summary>
public delegate Task<TResult> CommandHandlerDelegate<TResult>();

/// <summary>
/// Base interface for command middleware.
/// Middleware can intercept command execution for cross-cutting concerns.
/// </summary>
public interface ICommandMiddleware
{
    /// <summary>
    /// The order in which this middleware should execute (lower executes first)
    /// </summary>
    int Order { get; }
}

/// <summary>
/// Middleware for processing commands
/// </summary>
public interface ICommandMiddleware<TCommand> : ICommandMiddleware
    where TCommand : ICommand
{
    /// <summary>
    /// Invokes the middleware
    /// </summary>
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="command">The command being executed</param>
    /// <param name="context">The command execution context</param>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<TResult> InvokeAsync<TResult>(
        TCommand command,
        CommandContext context,
        CommandHandlerDelegate<TResult> next,
        CancellationToken cancellationToken);
}
