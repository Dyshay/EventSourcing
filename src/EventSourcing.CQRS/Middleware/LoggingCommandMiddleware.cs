using EventSourcing.CQRS.Commands;
using EventSourcing.CQRS.Context;
using Microsoft.Extensions.Logging;

namespace EventSourcing.CQRS.Middleware;

/// <summary>
/// Middleware that logs command execution details
/// </summary>
public class LoggingCommandMiddleware<TCommand> : ICommandMiddleware<TCommand>
    where TCommand : ICommand
{
    private readonly ILogger<LoggingCommandMiddleware<TCommand>> _logger;

    public int Order => 10; // Execute early in pipeline

    public LoggingCommandMiddleware(ILogger<LoggingCommandMiddleware<TCommand>> logger)
    {
        _logger = logger;
    }

    public async Task<TResult> InvokeAsync<TResult>(
        TCommand command,
        CommandContext context,
        CommandHandlerDelegate<TResult> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting command execution: {CommandType} (ID: {CommandId}, Correlation: {CorrelationId})",
            typeof(TCommand).Name,
            command.CommandId,
            context.CorrelationId);

        try
        {
            var result = await next();

            _logger.LogInformation(
                "Command executed successfully: {CommandType} (ID: {CommandId}, Duration: {Duration}ms, Events: {EventCount})",
                typeof(TCommand).Name,
                command.CommandId,
                context.ExecutionTimeMs,
                context.GeneratedEvents.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Command execution failed: {CommandType} (ID: {CommandId}, Error: {ErrorMessage})",
                typeof(TCommand).Name,
                command.CommandId,
                ex.Message);

            throw;
        }
    }
}
