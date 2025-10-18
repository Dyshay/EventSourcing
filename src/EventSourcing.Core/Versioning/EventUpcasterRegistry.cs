namespace EventSourcing.Core.Versioning;

using EventSourcing.Abstractions;
using EventSourcing.Abstractions.Versioning;

/// <summary>
/// Registry for managing event upcasters and performing event transformations
/// </summary>
public class EventUpcasterRegistry : IEventUpcasterRegistry
{
    private readonly Dictionary<Type, IEventUpcaster> _upcasters = [];

    /// <inheritdoc/>
    public void RegisterUpcaster(IEventUpcaster upcaster)
    {
        ArgumentNullException.ThrowIfNull(upcaster);

        if (_upcasters.ContainsKey(upcaster.SourceType))
        {
            throw new InvalidOperationException(
                $"An upcaster for source type {upcaster.SourceType.Name} is already registered");
        }

        _upcasters[upcaster.SourceType] = upcaster;
    }

    /// <inheritdoc/>
    public bool HasUpcaster(Type sourceType)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        return _upcasters.ContainsKey(sourceType);
    }

    /// <inheritdoc/>
    public IEvent UpcastToLatest(IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var currentEvent = @event;
        var maxIterations = 100; // Prevent infinite loops
        var iterations = 0;

        while (TryUpcastOnce(currentEvent, out var upcastedEvent))
        {
            currentEvent = upcastedEvent;
            iterations++;

            if (iterations >= maxIterations)
            {
                throw new InvalidOperationException(
                    $"Maximum upcasting iterations ({maxIterations}) reached for event type {@event.GetType().Name}. " +
                    "This may indicate a circular upcasting chain.");
            }
        }

        return currentEvent;
    }

    /// <inheritdoc/>
    public bool TryUpcastOnce(IEvent @event, out IEvent upcastedEvent)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var eventType = @event.GetType();

        if (_upcasters.TryGetValue(eventType, out var upcaster))
        {
            upcastedEvent = upcaster.Upcast(@event);
            return true;
        }

        upcastedEvent = @event;
        return false;
    }
}
