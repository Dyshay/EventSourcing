using System.Reflection;
using EventSourcing.Abstractions;
using EventSourcing.Core.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Core.Publishing;

/// <summary>
/// Default implementation of the event bus.
/// Dispatches events to projections (via Handle methods) and external publishers.
/// </summary>
public class EventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Type, List<Action<object, IEvent>>> _projectionHandlers = [];

    public EventBus(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        await PublishAsync([@event], cancellationToken);
    }

    public async Task PublishAsync(IEnumerable<IEvent> events, CancellationToken cancellationToken = default)
    {
        var eventsList = events.ToList();

        if (!eventsList.Any())
        {
            return;
        }

        // Publish to projections
        await PublishToProjectionsAsync(eventsList, cancellationToken);

        // Publish to external publishers
        await PublishToExternalPublishersAsync(eventsList, cancellationToken);
    }

    private async Task PublishToProjectionsAsync(List<IEvent> events, CancellationToken cancellationToken)
    {
        var projections = _serviceProvider.GetServices<IProjection>();

        foreach (var projection in projections)
        {
            foreach (var @event in events)
            {
                await InvokeProjectionHandlerAsync(projection, @event, cancellationToken);
            }
        }
    }

    private async Task PublishToExternalPublishersAsync(List<IEvent> events, CancellationToken cancellationToken)
    {
        var publishers = _serviceProvider.GetServices<IEventPublisher>();

        foreach (var publisher in publishers)
        {
            foreach (var @event in events)
            {
                await publisher.PublishAsync(@event, cancellationToken);
            }
        }
    }

    private async Task InvokeProjectionHandlerAsync(IProjection projection, IEvent @event, CancellationToken cancellationToken)
    {
        var eventType = @event.GetType();
        var projectionType = projection.GetType();

        // Look for Handle(TEvent) method
        var method = projectionType.GetMethod(
            "Handle",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            [eventType, typeof(CancellationToken)],
            null);

        if (method == null)
        {
            // Try without cancellation token
            method = projectionType.GetMethod(
                "Handle",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                [eventType],
                null);
        }

        if (method != null)
        {
            var result = method.Invoke(projection, method.GetParameters().Length == 2
                ? [@event, cancellationToken]
                : [@event]);

            if (result is Task task)
            {
                await task;
            }
        }
    }
}
