namespace EventSourcing.Core.Projections;

/// <summary>
/// Marker interface for projections.
/// Projections are denormalized read models that are updated in response to events.
/// </summary>
public interface IProjection
{
    // Marker interface - implementations will have Handle(TEvent) methods
    // discovered via reflection or explicit registration
}
