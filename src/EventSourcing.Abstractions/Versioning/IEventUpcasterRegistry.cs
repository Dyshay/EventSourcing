namespace EventSourcing.Abstractions.Versioning;

/// <summary>
/// Registry for managing event upcasters and performing event transformations
/// </summary>
public interface IEventUpcasterRegistry
{
    /// <summary>
    /// Register an upcaster instance
    /// </summary>
    /// <param name="upcaster">The upcaster to register</param>
    void RegisterUpcaster(IEventUpcaster upcaster);

    /// <summary>
    /// Check if an upcaster exists for the given source type
    /// </summary>
    /// <param name="sourceType">The source event type</param>
    /// <returns>True if an upcaster is registered</returns>
    bool HasUpcaster(Type sourceType);

    /// <summary>
    /// Upcast an event to its latest version by applying all upcasters in the chain
    /// </summary>
    /// <param name="event">The event to upcast</param>
    /// <returns>The event in its latest version</returns>
    IEvent UpcastToLatest(IEvent @event);

    /// <summary>
    /// Try to upcast an event one step (to the next version)
    /// </summary>
    /// <param name="event">The event to upcast</param>
    /// <param name="upcastedEvent">The upcasted event if successful</param>
    /// <returns>True if the event was upcasted</returns>
    bool TryUpcastOnce(IEvent @event, out IEvent upcastedEvent);
}
