using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EventSourcing.MongoDB.Models;

/// <summary>
/// MongoDB document model for storing aggregate snapshots.
/// </summary>
internal class SnapshotDocument
{
    /// <summary>
    /// MongoDB document identifier.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? MongoId { get; set; }

    /// <summary>
    /// Identifier of the aggregate this snapshot represents.
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
    /// Version of the aggregate at the time of the snapshot.
    /// </summary>
    [BsonElement("version")]
    public required int Version { get; init; }

    /// <summary>
    /// Timestamp when the snapshot was created.
    /// </summary>
    [BsonElement("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Serialized aggregate data (JSON).
    /// </summary>
    [BsonElement("data")]
    [BsonRepresentation(BsonType.String)]
    public required string Data { get; init; }
}
