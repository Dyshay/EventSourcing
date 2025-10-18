using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EventSourcing.MongoDB.Models;

/// <summary>
/// MongoDB document model for storing events.
/// </summary>
internal class EventDocument
{
    /// <summary>
    /// MongoDB document identifier.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? MongoId { get; set; }

    /// <summary>
    /// Identifier of the aggregate this event belongs to.
    /// </summary>
    [BsonElement("aggregateId")]
    [BsonRepresentation(BsonType.String)]
    public required string AggregateId { get; init; }

    /// <summary>
    /// Type name of the aggregate.
    /// </summary>
    [BsonElement("aggregateType")]
    public required string AggregateType { get; init; }

    /// <summary>
    /// Version/sequence number of this event within the aggregate's event stream.
    /// </summary>
    [BsonElement("version")]
    public required int Version { get; init; }

    /// <summary>
    /// Type name of the event for deserialization.
    /// </summary>
    [BsonElement("eventType")]
    public required string EventType { get; init; }

    /// <summary>
    /// Kind/category of the event (e.g., "user.created", "order.placed").
    /// </summary>
    [BsonElement("kind")]
    public required string Kind { get; init; }

    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    [BsonElement("eventId")]
    [BsonRepresentation(BsonType.String)]
    public required Guid EventId { get; init; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    [BsonElement("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Serialized event data (JSON).
    /// </summary>
    [BsonElement("data")]
    [BsonRepresentation(BsonType.String)]
    public required string Data { get; init; }
}
