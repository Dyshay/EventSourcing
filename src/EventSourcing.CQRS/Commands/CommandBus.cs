using System.Collections.Concurrent;
using EventSourcing.Abstractions;
using EventSourcing.CQRS.Configuration;
using EventSourcing.CQRS.Context;
using EventSourcing.CQRS.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

namespace EventSourcing.CQRS.Commands;

/// <summary>
/// Default implementation of command bus with middleware pipeline support
/// </summary>
public partial class CommandBus : ICommandBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandContextAccessor _contextAccessor;
    private readonly ILogger<CommandBus> _logger;
    private readonly CqrsOptions _options;
    private readonly ObjectPool<CommandContext> _contextPool;

    // Cache pour éviter la création répétée de types génériques
    private readonly ConcurrentDictionary<(Type CommandType, Type EventType), Type> _handlerTypeCache = new();

    public CommandBus(
        IServiceProvider serviceProvider,
        ICommandContextAccessor contextAccessor,
        ILogger<CommandBus> logger,
        IOptions<CqrsOptions> options,
        ObjectPool<CommandContext> contextPool)
    {
        _serviceProvider = serviceProvider;
        _contextAccessor = contextAccessor;
        _logger = logger;
        _options = options.Value;
        _contextPool = contextPool;
    }

    public async Task<CommandResult<TEvent>> SendAsync<TEvent>(
        ICommand<TEvent> command,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        var commandType = command.GetType();

        // Utiliser le cache pour éviter MakeGenericType à chaque appel
        var handlerType = _handlerTypeCache.GetOrAdd(
            (commandType, typeof(TEvent)),
            key => typeof(ICommandHandler<,>).MakeGenericType(key.CommandType, key.EventType));

        var handler = _serviceProvider.GetService(handlerType);

        if (handler == null)
        {
            throw new InvalidOperationException(
                $"No handler registered for command type {commandType.Name}");
        }

        // Get command context from pool (only if audit trail is enabled)
        CommandContext? context = null;
        if (_options.EnableAuditTrail)
        {
            context = _contextPool.Get();
            context.Initialize(command);
            _contextAccessor.CurrentContext = context;
        }

        try
        {
            if (_options.EnableLogging)
            {
                LogCommandExecution(commandType.Name, command.CommandId);
            }

            // Build and execute middleware pipeline
            var result = await ExecuteWithMiddleware<TEvent>(command, context, handler, cancellationToken);

            // Record events in context (only if audit trail is enabled)
            if (context != null)
            {
                if (result.Success && result.Data != null)
                {
                    context.RecordEvent(result.Data);
                }

                context.MarkSuccess(result.AggregateId, result.Version);

                if (_options.EnableLogging)
                {
                    LogCommandSuccess(commandType.Name, context.ExecutionTimeMs);
                }
            }
            else if (_options.EnableLogging)
            {
                LogCommandSuccessNoTiming(commandType.Name);
            }

            return result;
        }
        catch (Exception ex)
        {
            if (context != null)
            {
                context.MarkFailure(ex.Message);
            }

            if (_options.EnableLogging)
            {
                LogCommandError(ex, commandType.Name, ex.Message);
            }

            return CommandResult<TEvent>.FailureResult(ex.Message)!;
        }
        finally
        {
            if (_options.EnableAuditTrail && context != null)
            {
                _contextAccessor.CurrentContext = null;
                _contextPool.Return(context);
            }
        }
    }

    public async Task<CommandResult<IEnumerable<IEvent>>> SendAsync(
        ICommandMultiEvent command,
        CancellationToken cancellationToken = default)
    {
        var commandType = command.GetType();
        var handlerType = typeof(ICommandHandlerMultiEvent<>).MakeGenericType(commandType);
        var handler = _serviceProvider.GetService(handlerType);

        if (handler == null)
        {
            throw new InvalidOperationException(
                $"No handler registered for command type {commandType.Name}");
        }

        // Get command context from pool (only if audit trail is enabled)
        CommandContext? context = null;
        if (_options.EnableAuditTrail)
        {
            context = _contextPool.Get();
            context.Initialize(command);
            _contextAccessor.CurrentContext = context;
        }

        try
        {
            if (_options.EnableLogging)
            {
                LogMultiEventCommandExecution(commandType.Name, command.CommandId);
            }

            var result = await ExecuteWithMiddleware<IEnumerable<IEvent>>(command, context, handler, cancellationToken);

            // Record events in context (only if audit trail is enabled)
            if (context != null)
            {
                if (result.Success && result.Data != null)
                {
                    context.RecordEvents(result.Data);
                }

                context.MarkSuccess(result.AggregateId, result.Version);

                if (_options.EnableLogging)
                {
                    LogMultiEventCommandSuccess(commandType.Name, context.GeneratedEvents.Count, context.ExecutionTimeMs);
                }
            }
            else if (_options.EnableLogging)
            {
                LogMultiEventCommandSuccessNoTiming(commandType.Name);
            }

            return result;
        }
        catch (Exception ex)
        {
            if (context != null)
            {
                context.MarkFailure(ex.Message);
            }

            if (_options.EnableLogging)
            {
                LogMultiEventCommandError(ex, commandType.Name, ex.Message);
            }

            return CommandResult<IEnumerable<IEvent>>.FailureResult(ex.Message)!;
        }
        finally
        {
            if (_options.EnableAuditTrail && context != null)
            {
                _contextAccessor.CurrentContext = null;
                _contextPool.Return(context);
            }
        }
    }

    private async Task<CommandResult<TResult>> ExecuteWithMiddleware<TResult>(
        ICommand command,
        CommandContext? context,
        object handler,
        CancellationToken cancellationToken)
    {
        // Get all middleware for this command type
        var commandType = command.GetType();
        var middlewareType = typeof(ICommandMiddleware<>).MakeGenericType(commandType);
        var middleware = _serviceProvider.GetServices(middlewareType)
            .Cast<ICommandMiddleware>()
            .OrderBy(m => m.Order)
            .ToList();

        // Build the pipeline
        CommandHandlerDelegate<CommandResult<TResult>> pipeline = async () =>
        {
            // Final step: invoke the actual handler
            var handleMethod = handler.GetType().GetMethod("HandleAsync");
            if (handleMethod == null)
            {
                throw new InvalidOperationException("Handler does not have HandleAsync method");
            }

            var task = (Task<CommandResult<TResult>>)handleMethod.Invoke(
                handler,
                new object[] { command, cancellationToken })!;

            return await task;
        };

        // Wrap with middleware in reverse order
        foreach (var mw in middleware.AsEnumerable().Reverse())
        {
            var currentPipeline = pipeline;
            var currentMiddleware = mw;

            pipeline = async () =>
            {
                var invokeMethod = currentMiddleware.GetType()
                    .GetMethod("InvokeAsync");

                if (invokeMethod == null)
                {
                    throw new InvalidOperationException(
                        $"Middleware {currentMiddleware.GetType().Name} does not have InvokeAsync method");
                }

                var task = (Task<CommandResult<TResult>>)invokeMethod.Invoke(
                    currentMiddleware,
                    new object[] { command, context, currentPipeline, cancellationToken })!;

                return await task;
            };
        }

        return await pipeline();
    }

    // LoggerMessage source generators for better performance
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Executing command {CommandType} with ID {CommandId}")]
    private partial void LogCommandExecution(string commandType, Guid commandId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Command {CommandType} executed successfully in {ExecutionTime}ms")]
    private partial void LogCommandSuccess(string commandType, long executionTime);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Command {CommandType} executed successfully")]
    private partial void LogCommandSuccessNoTiming(string commandType);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error,
        Message = "Command {CommandType} failed: {ErrorMessage}")]
    private partial void LogCommandError(Exception ex, string commandType, string errorMessage);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "Executing multi-event command {CommandType} with ID {CommandId}")]
    private partial void LogMultiEventCommandExecution(string commandType, Guid commandId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information,
        Message = "Multi-event command {CommandType} executed successfully, generated {EventCount} events in {ExecutionTime}ms")]
    private partial void LogMultiEventCommandSuccess(string commandType, int eventCount, long executionTime);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information,
        Message = "Multi-event command {CommandType} executed successfully")]
    private partial void LogMultiEventCommandSuccessNoTiming(string commandType);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error,
        Message = "Multi-event command {CommandType} failed: {ErrorMessage}")]
    private partial void LogMultiEventCommandError(Exception ex, string commandType, string errorMessage);
}
