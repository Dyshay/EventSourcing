using System.Diagnostics;
using EventSourcing.CQRS.Commands;
using EventSourcing.CQRS.Context;
using Microsoft.Extensions.Logging;

namespace EventSourcing.CQRS.Middleware;

/// <summary>
/// Middleware that collects performance metrics for command execution
/// </summary>
public class MetricsCommandMiddleware<TCommand> : ICommandMiddleware<TCommand>
    where TCommand : ICommand
{
    private readonly ILogger<MetricsCommandMiddleware<TCommand>> _logger;

    public int Order => 5; // Execute very early in pipeline

    public MetricsCommandMiddleware(ILogger<MetricsCommandMiddleware<TCommand>> logger)
    {
        _logger = logger;
    }

    public async Task<TResult> InvokeAsync<TResult>(
        TCommand command,
        CommandContext context,
        CommandHandlerDelegate<TResult> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await next();
            stopwatch.Stop();

            // Record metrics
            context.Metadata["ExecutionTimeMs"] = stopwatch.ElapsedMilliseconds;
            context.Metadata["Success"] = true;

            _logger.LogDebug(
                "Command metrics - Type: {CommandType}, Duration: {Duration}ms, Success: true",
                typeof(TCommand).Name,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception)
        {
            stopwatch.Stop();

            context.Metadata["ExecutionTimeMs"] = stopwatch.ElapsedMilliseconds;
            context.Metadata["Success"] = false;

            _logger.LogDebug(
                "Command metrics - Type: {CommandType}, Duration: {Duration}ms, Success: false",
                typeof(TCommand).Name,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
