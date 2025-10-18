using System.Collections.Concurrent;
using System.Text.Json;
using EventSourcing.Abstractions;

namespace EventSourcing.MongoDB.Serialization;

/// <summary>
/// Handles serialization and deserialization of events with type registry support.
/// </summary>
internal class EventSerializer
{
    private static readonly ConcurrentDictionary<string, Type> TypeRegistry = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Registers an event type for deserialization.
    /// </summary>
    /// <param name="eventType">The event type to register</param>
    public static void RegisterEventType(Type eventType)
    {
        if (!typeof(IEvent).IsAssignableFrom(eventType))
        {
            throw new ArgumentException($"Type {eventType.Name} must implement IEvent", nameof(eventType));
        }

        TypeRegistry.TryAdd(eventType.Name, eventType);
    }

    /// <summary>
    /// Serializes an event to JSON.
    /// </summary>
    /// <param name="event">The event to serialize</param>
    /// <returns>JSON string representation of the event</returns>
    public string Serialize(IEvent @event)
    {
        return JsonSerializer.Serialize(@event, @event.GetType(), JsonOptions);
    }

    /// <summary>
    /// Deserializes an event from JSON.
    /// </summary>
    /// <param name="eventType">The event type name</param>
    /// <param name="data">JSON data</param>
    /// <returns>Deserialized event</returns>
    /// <exception cref="InvalidOperationException">Thrown when event type is not registered</exception>
    public IEvent Deserialize(string eventType, string data)
    {
        if (!TypeRegistry.TryGetValue(eventType, out var type))
        {
            throw new InvalidOperationException(
                $"Event type '{eventType}' is not registered. " +
                $"Make sure to register all event types during application startup.");
        }

        var @event = JsonSerializer.Deserialize(data, type, JsonOptions) as IEvent;

        if (@event == null)
        {
            throw new InvalidOperationException($"Failed to deserialize event of type '{eventType}'");
        }

        return @event;
    }
}
