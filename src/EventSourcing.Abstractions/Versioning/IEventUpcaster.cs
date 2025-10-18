namespace EventSourcing.Abstractions.Versioning;

/// <summary>
/// Transforms an old event version to a new version.
/// This enables event schema evolution over time.
/// </summary>
public interface IEventUpcaster
{
    /// <summary>
    /// Type of the old event that this upcaster can transform
    /// </summary>
    Type SourceType { get; }

    /// <summary>
    /// Type of the new event that this upcaster produces
    /// </summary>
    Type TargetType { get; }

    /// <summary>
    /// Transform the old event to the new version
    /// </summary>
    /// <param name="oldEvent">The old event to transform</param>
    /// <returns>The new event version</returns>
    IEvent Upcast(IEvent oldEvent);
}

/// <summary>
/// Generic typed upcaster for type-safe event transformation
/// </summary>
/// <typeparam name="TSource">The old event type</typeparam>
/// <typeparam name="TTarget">The new event type</typeparam>
public interface IEventUpcaster<in TSource, out TTarget> : IEventUpcaster
    where TSource : IEvent
    where TTarget : IEvent
{
    /// <summary>
    /// Transform the old event to the new version (typed)
    /// </summary>
    /// <param name="oldEvent">The old event to transform</param>
    /// <returns>The new event version</returns>
    TTarget Upcast(TSource oldEvent);
}
