using System.Reflection;
using EventSourcing.Abstractions;

namespace EventSourcing.Core;

/// <summary>
/// Base class for aggregates providing event sourcing infrastructure.
/// Handles event replay, uncommitted events tracking, and version management.
/// </summary>
/// <typeparam name="TId">Type of the aggregate identifier</typeparam>
public abstract class AggregateBase<TId> : IAggregate<TId> where TId : notnull
{
    private readonly List<IEvent> _uncommittedEvents = new();
    private readonly Dictionary<Type, MethodInfo> _eventHandlers = new();

    protected AggregateBase()
    {
        Version = 0;
        CacheEventHandlers();
    }

    public abstract TId Id { get; protected set; }
    public int Version { get; set; }
    public IReadOnlyList<IEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    /// <summary>
    /// Raises a new domain event. The event will be applied to the aggregate state
    /// and added to the uncommitted events list.
    /// </summary>
    /// <param name="event">The domain event to raise</param>
    public void RaiseEvent(IEvent @event)
    {
        ApplyEvent(@event, isNew: true);
        _uncommittedEvents.Add(@event);
    }

    /// <summary>
    /// Marks all uncommitted events as committed by clearing the uncommitted events list.
    /// </summary>
    public void MarkEventsAsCommitted()
    {
        _uncommittedEvents.Clear();
    }

    /// <summary>
    /// Loads the aggregate from historical events by replaying them.
    /// </summary>
    /// <param name="events">Historical events to replay</param>
    public void LoadFromHistory(IEnumerable<IEvent> events)
    {
        foreach (var @event in events)
        {
            ApplyEvent(@event, isNew: false);
            Version++;
        }
    }

    /// <summary>
    /// Applies an event to the aggregate by invoking the appropriate Apply method.
    /// Uses reflection to find and invoke Apply(TEvent) methods.
    /// </summary>
    /// <param name="event">The event to apply</param>
    /// <param name="isNew">Whether this is a new event (true) or historical (false)</param>
    private void ApplyEvent(IEvent @event, bool isNew)
    {
        var eventType = @event.GetType();

        if (_eventHandlers.TryGetValue(eventType, out var handler))
        {
            handler.Invoke(this, new object[] { @event });
        }
        else
        {
            // No Apply method found - this might be intentional for some events
            // Don't throw, just log or ignore
        }
    }

    /// <summary>
    /// Caches all Apply methods for faster event replay.
    /// Scans for methods named "Apply" with a single parameter of type IEvent.
    /// </summary>
    private void CacheEventHandlers()
    {
        var aggregateType = GetType();
        var methods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var method in methods)
        {
            if (method.Name == "Apply" && method.GetParameters().Length == 1)
            {
                var parameter = method.GetParameters()[0];
                if (typeof(IEvent).IsAssignableFrom(parameter.ParameterType))
                {
                    _eventHandlers[parameter.ParameterType] = method;
                }
            }
        }
    }
}
