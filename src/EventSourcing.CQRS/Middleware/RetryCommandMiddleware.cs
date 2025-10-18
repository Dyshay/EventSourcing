using EventSourcing.CQRS.Commands;
using EventSourcing.CQRS.Context;
using Microsoft.Extensions.Logging;

namespace EventSourcing.CQRS.Middleware;

/// <summary>
/// Middleware that retries command execution on transient failures
/// </summary>
public class RetryCommandMiddleware<TCommand> : ICommandMiddleware<TCommand>
    where TCommand : ICommand
{
    private readonly ILogger<RetryCommandMiddleware<TCommand>> _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay;

    public int Order => 15; // Execute after logging but before validation

    public RetryCommandMiddleware(
        ILogger<RetryCommandMiddleware<TCommand>> logger,
        int maxRetries = 3,
        TimeSpan? retryDelay = null)
    {
        _logger = logger;
        _maxRetries = maxRetries;
        _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(100);
    }

    public async Task<TResult> InvokeAsync<TResult>(
        TCommand command,
        CommandContext context,
        CommandHandlerDelegate<TResult> next,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _maxRetries)
        {
            try
            {
                return await next();
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < _maxRetries - 1)
            {
                lastException = ex;
                attempt++;

                var delay = _retryDelay * Math.Pow(2, attempt - 1); // Exponential backoff

                _logger.LogWarning(
                    ex,
                    "Command {CommandType} failed with transient error. Retrying attempt {Attempt}/{MaxRetries} after {Delay}ms",
                    typeof(TCommand).Name,
                    attempt,
                    _maxRetries,
                    delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }

        // If we get here, all retries failed
        _logger.LogError(
            lastException,
            "Command {CommandType} failed after {MaxRetries} retries",
            typeof(TCommand).Name,
            _maxRetries);

        throw lastException!;
    }

    private static bool IsTransientException(Exception ex)
    {
        // Add your transient exception detection logic here
        // For example: network errors, temporary database unavailability, etc.
        return ex is TimeoutException
            || ex is System.Net.Http.HttpRequestException
            || (ex.InnerException != null && IsTransientException(ex.InnerException));
    }
}
