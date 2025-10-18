using EventSourcing.Abstractions;
using EventSourcing.CQRS.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventSourcing.CQRS.Events;

/// <summary>
/// Default implementation of the enhanced event bus
/// </summary>
public class EventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventBus> _logger;
    private readonly IQueryCache? _queryCache;
    private readonly IEventStreamPublisher? _streamPublisher;

    public EventBus(
        IServiceProvider serviceProvider,
        ILogger<EventBus> logger,
        IQueryCache? queryCache = null,
        IEventStreamPublisher? streamPublisher = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _queryCache = queryCache;
        _streamPublisher = streamPublisher;
    }

    public async Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Publishing event {EventType} (ID: {EventId})",
            @event.EventType,
            @event.EventId);

        try
        {
            // Get all event handlers for this event type
            var handlerType = typeof(IEventHandler<>).MakeGenericType(@event.GetType());
            var handlers = _serviceProvider.GetServices(handlerType);

            var tasks = new List<Task>();

            foreach (var handler in handlers)
            {
                var handleMethod = handler?.GetType().GetMethod("HandleAsync");
                if (handleMethod != null)
                {
                    var invokeResult = handleMethod.Invoke(handler, new object[] { @event, cancellationToken });
                    if (invokeResult != null)
                    {
                        var task = (Task)invokeResult;
                        tasks.Add(task);
                    }
                }
            }

            await Task.WhenAll(tasks);

            // Invalidate query cache based on event type
            if (_queryCache != null)
            {
                await _queryCache.InvalidateByEventAsync(@event.EventType, cancellationToken);
            }

            _logger.LogInformation(
                "Event {EventType} published to {HandlerCount} handlers",
                @event.EventType,
                tasks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error publishing event {EventType}: {ErrorMessage}",
                @event.EventType,
                ex.Message);

            throw;
        }
    }

    public async Task PublishBatchAsync(IEnumerable<IEvent> events, CancellationToken cancellationToken = default)
    {
        var eventsList = events.ToList();

        _logger.LogInformation(
            "Publishing batch of {EventCount} events",
            eventsList.Count);

        foreach (var @event in eventsList)
        {
            await PublishAsync(@event, cancellationToken);
        }
    }

    public async Task PublishToStreamAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        if (_streamPublisher == null)
        {
            _logger.LogWarning(
                "No stream publisher configured. Event {EventType} will not be published to stream.",
                @event.EventType);
            return;
        }

        _logger.LogDebug(
            "Publishing event {EventType} to stream",
            @event.EventType);

        try
        {
            await _streamPublisher.PublishAsync(@event, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error publishing event {EventType} to stream: {ErrorMessage}",
                @event.EventType,
                ex.Message);

            throw;
        }
    }

    public async Task PublishBatchToStreamAsync(IEnumerable<IEvent> events, CancellationToken cancellationToken = default)
    {
        if (_streamPublisher == null)
        {
            _logger.LogWarning("No stream publisher configured. Events will not be published to stream.");
            return;
        }

        var eventsList = events.ToList();

        _logger.LogDebug(
            "Publishing batch of {EventCount} events to stream",
            eventsList.Count);

        try
        {
            await _streamPublisher.PublishBatchAsync(eventsList, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error publishing event batch to stream: {ErrorMessage}",
                ex.Message);

            throw;
        }
    }
}
