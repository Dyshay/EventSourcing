namespace EventSourcing.Core.Versioning;

using EventSourcing.Abstractions;
using EventSourcing.Abstractions.Versioning;

/// <summary>
/// Base class for implementing strongly-typed event upcasters
/// </summary>
/// <typeparam name="TSource">The old event type to transform from</typeparam>
/// <typeparam name="TTarget">The new event type to transform to</typeparam>
public abstract class EventUpcaster<TSource, TTarget> : IEventUpcaster<TSource, TTarget>
    where TSource : IEvent
    where TTarget : IEvent
{
    /// <inheritdoc/>
    public Type SourceType => typeof(TSource);

    /// <inheritdoc/>
    public Type TargetType => typeof(TTarget);

    /// <inheritdoc/>
    public abstract TTarget Upcast(TSource oldEvent);

    /// <inheritdoc/>
    IEvent IEventUpcaster.Upcast(IEvent oldEvent)
    {
        if (oldEvent is not TSource typedEvent)
        {
            throw new InvalidOperationException(
                $"Cannot upcast event of type {oldEvent.GetType().Name} to {typeof(TSource).Name}");
        }

        return Upcast(typedEvent);
    }
}
