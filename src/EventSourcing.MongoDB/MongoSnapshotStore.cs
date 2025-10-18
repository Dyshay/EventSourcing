using System.Text.Json;
using EventSourcing.Abstractions;
using EventSourcing.MongoDB.Models;
using MongoDB.Driver;

namespace EventSourcing.MongoDB;

/// <summary>
/// MongoDB implementation of the snapshot store.
/// Stores snapshots in collections named {aggregateType}_snapshots.
/// </summary>
public class MongoSnapshotStore : ISnapshotStore
{
    private readonly IMongoDatabase _database;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        IncludeFields = true, // Include backing fields for properties with private setters
        PropertyNameCaseInsensitive = true
    };

    public MongoSnapshotStore(IMongoDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task SaveSnapshotAsync<TId, TAggregate>(
        TId aggregateId,
        string aggregateType,
        TAggregate aggregate,
        int version,
        CancellationToken cancellationToken = default)
        where TId : notnull
        where TAggregate : IAggregate<TId>
    {
        var collection = GetSnapshotCollection(aggregateType);
        var aggregateIdStr = aggregateId.ToString()!;

        var document = new SnapshotDocument
        {
            AggregateId = aggregateIdStr,
            AggregateType = aggregateType,
            Version = version,
            Timestamp = DateTimeOffset.UtcNow,
            Data = JsonSerializer.Serialize(aggregate, typeof(TAggregate), JsonOptions)
        };

        // Replace or insert the snapshot (keep only the latest)
        var filter = Builders<SnapshotDocument>.Filter.And(
            Builders<SnapshotDocument>.Filter.Eq(s => s.AggregateId, aggregateIdStr),
            Builders<SnapshotDocument>.Filter.Eq(s => s.AggregateType, aggregateType)
        );

        await collection.ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<Snapshot<TAggregate>?> GetLatestSnapshotAsync<TId, TAggregate>(
        TId aggregateId,
        string aggregateType,
        CancellationToken cancellationToken = default)
        where TId : notnull
        where TAggregate : IAggregate<TId>
    {
        var collection = GetSnapshotCollection(aggregateType);
        var aggregateIdStr = aggregateId.ToString()!;

        var filter = Builders<SnapshotDocument>.Filter.And(
            Builders<SnapshotDocument>.Filter.Eq(s => s.AggregateId, aggregateIdStr),
            Builders<SnapshotDocument>.Filter.Eq(s => s.AggregateType, aggregateType)
        );

        var document = await collection
            .Find(filter)
            .SortByDescending(s => s.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (document == null)
        {
            return null;
        }

        var aggregate = JsonSerializer.Deserialize<TAggregate>(document.Data, JsonOptions);

        if (aggregate == null)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize snapshot for aggregate '{aggregateId}' of type '{aggregateType}'");
        }

        return new Snapshot<TAggregate>(aggregate, document.Version, document.Timestamp);
    }

    private IMongoCollection<SnapshotDocument> GetSnapshotCollection(string aggregateType)
    {
        var collectionName = $"{aggregateType.ToLowerInvariant()}_snapshots";
        return _database.GetCollection<SnapshotDocument>(collectionName);
    }

    /// <summary>
    /// Ensures indexes are created for optimal query performance.
    /// Should be called during application startup.
    /// </summary>
    /// <param name="aggregateTypes">Types of aggregates to create indexes for</param>
    public async Task EnsureIndexesAsync(params string[] aggregateTypes)
    {
        foreach (var aggregateType in aggregateTypes)
        {
            var collection = GetSnapshotCollection(aggregateType);

            // Compound index for aggregateId + aggregateType (unique)
            var aggregateIndex = Builders<SnapshotDocument>.IndexKeys
                .Ascending(s => s.AggregateId)
                .Ascending(s => s.AggregateType);

            var indexModel = new CreateIndexModel<SnapshotDocument>(
                aggregateIndex,
                new CreateIndexOptions { Unique = true });

            await collection.Indexes.CreateOneAsync(indexModel);
        }
    }
}
