using System.Text.Json;
using EventSourcing.Abstractions;
using EventSourcing.Abstractions.Versioning;
using EventSourcing.MongoDB.Models;
using EventSourcing.MongoDB.Serialization;
using MongoDB.Driver;

namespace EventSourcing.MongoDB;

/// <summary>
/// MongoDB implementation of the event store.
/// Stores events in collections named {aggregateType}_events.
/// </summary>
public class MongoEventStore : IEventStore
{
    private readonly IMongoDatabase _database;
    private readonly EventSerializer _serializer;
    private readonly IEventUpcasterRegistry? _upcasterRegistry;

    public MongoEventStore(IMongoDatabase database, IEventUpcasterRegistry? upcasterRegistry = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _serializer = new EventSerializer();
        _upcasterRegistry = upcasterRegistry;
    }

    public async Task AppendEventsAsync<TId>(
        TId aggregateId,
        string aggregateType,
        IEnumerable<IEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken = default) where TId : notnull
    {
        if (string.IsNullOrEmpty(aggregateType))
            throw new ArgumentException("Aggregate type cannot be null or empty", nameof(aggregateType));

        if (events == null)
            throw new ArgumentNullException(nameof(events));

        var collection = GetEventCollection(aggregateType);
        var eventsList = events.ToList();

        if (!eventsList.Any())
        {
            return;
        }

        // Check for concurrency conflicts
        var aggregateIdStr = aggregateId.ToString()!;
        var currentVersion = await GetCurrentVersionAsync(collection, aggregateIdStr, cancellationToken);

        if (currentVersion != expectedVersion)
        {
            throw new ConcurrencyException(aggregateId, expectedVersion, currentVersion);
        }

        // Create event documents
        var documents = new List<EventDocument>();
        var version = expectedVersion;

        foreach (var @event in eventsList)
        {
            if (@event == null)
                throw new ArgumentException("Event collection contains null event", nameof(events));

            version++;
            var document = new EventDocument
            {
                AggregateId = aggregateIdStr,
                AggregateType = aggregateType,
                Version = version,
                EventType = @event.EventType ?? throw new InvalidOperationException($"Event {nameof(@event.EventType)} cannot be null"),
                Kind = @event.Kind ?? throw new InvalidOperationException($"Event {nameof(@event.Kind)} cannot be null"),
                EventId = @event.EventId,
                Timestamp = @event.Timestamp,
                Data = _serializer.Serialize(@event)
            };
            documents.Add(document);
        }

        // Insert all events atomically
        try
        {
            await collection.InsertManyAsync(documents, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Concurrency conflict detected
            throw new ConcurrencyException(
                $"Concurrency conflict detected when appending events for aggregate '{aggregateId}'", ex);
        }
    }

    public async Task<IEnumerable<IEvent>> GetEventsAsync<TId>(
        TId aggregateId,
        string aggregateType,
        CancellationToken cancellationToken = default) where TId : notnull
    {
        return await GetEventsAsync(aggregateId, aggregateType, fromVersion: 0, cancellationToken);
    }

    public async Task<IEnumerable<IEvent>> GetEventsAsync<TId>(
        TId aggregateId,
        string aggregateType,
        int fromVersion,
        CancellationToken cancellationToken = default) where TId : notnull
    {
        var collection = GetEventCollection(aggregateType);
        var aggregateIdStr = aggregateId.ToString()!;

        var filter = Builders<EventDocument>.Filter.And(
            Builders<EventDocument>.Filter.Eq(e => e.AggregateId, aggregateIdStr),
            Builders<EventDocument>.Filter.Gt(e => e.Version, fromVersion)
        );

        var documents = await collection
            .Find(filter)
            .SortBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return documents.Select(doc => DeserializeAndUpcast(doc.EventType, doc.Data)).ToList();
    }

    private IMongoCollection<EventDocument> GetEventCollection(string aggregateType)
    {
        var collectionName = $"{aggregateType.ToLowerInvariant()}_events";
        return _database.GetCollection<EventDocument>(collectionName);
    }

    private async Task<int> GetCurrentVersionAsync(
        IMongoCollection<EventDocument> collection,
        string aggregateId,
        CancellationToken cancellationToken)
    {
        var filter = Builders<EventDocument>.Filter.Eq(e => e.AggregateId, aggregateId);
        var lastEvent = await collection
            .Find(filter)
            .SortByDescending(e => e.Version)
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);

        return lastEvent?.Version ?? 0;
    }

    public async Task<IEnumerable<IEvent>> GetAllEventsAsync(
        string aggregateType,
        CancellationToken cancellationToken = default)
    {
        var collection = GetEventCollection(aggregateType);

        var documents = await collection
            .Find(Builders<EventDocument>.Filter.Empty)
            .SortBy(e => e.Timestamp)
            .ThenBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return documents.Select(doc => DeserializeAndUpcast(doc.EventType, doc.Data)).ToList();
    }

    public async Task<IEnumerable<IEvent>> GetAllEventsAsync(
        string aggregateType,
        DateTimeOffset fromTimestamp,
        CancellationToken cancellationToken = default)
    {
        var collection = GetEventCollection(aggregateType);

        var filter = Builders<EventDocument>.Filter.Gte(e => e.Timestamp, fromTimestamp);

        var documents = await collection
            .Find(filter)
            .SortBy(e => e.Timestamp)
            .ThenBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return documents.Select(doc => DeserializeAndUpcast(doc.EventType, doc.Data)).ToList();
    }

    public async Task<IEnumerable<IEvent>> GetEventsByKindAsync(
        string aggregateType,
        string kind,
        CancellationToken cancellationToken = default)
    {
        var collection = GetEventCollection(aggregateType);

        var filter = Builders<EventDocument>.Filter.Eq(e => e.Kind, kind);

        var documents = await collection
            .Find(filter)
            .SortBy(e => e.Timestamp)
            .ThenBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return documents.Select(doc => DeserializeAndUpcast(doc.EventType, doc.Data)).ToList();
    }

    public async Task<IEnumerable<IEvent>> GetEventsByKindsAsync(
        string aggregateType,
        IEnumerable<string> kinds,
        CancellationToken cancellationToken = default)
    {
        var collection = GetEventCollection(aggregateType);

        var kindsList = kinds.ToList();
        var filter = Builders<EventDocument>.Filter.In(e => e.Kind, kindsList);

        var documents = await collection
            .Find(filter)
            .SortBy(e => e.Timestamp)
            .ThenBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return documents.Select(doc => DeserializeAndUpcast(doc.EventType, doc.Data)).ToList();
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
            var collection = GetEventCollection(aggregateType);

            // Compound index for aggregateId + version (unique)
            var aggregateVersionIndex = Builders<EventDocument>.IndexKeys
                .Ascending(e => e.AggregateId)
                .Ascending(e => e.Version);

            var indexModel = new CreateIndexModel<EventDocument>(
                aggregateVersionIndex,
                new CreateIndexOptions { Unique = true });

            await collection.Indexes.CreateOneAsync(indexModel);

            // Index on timestamp for GetAllEventsAsync queries
            var timestampIndex = Builders<EventDocument>.IndexKeys
                .Ascending(e => e.Timestamp);

            var timestampIndexModel = new CreateIndexModel<EventDocument>(timestampIndex);

            await collection.Indexes.CreateOneAsync(timestampIndexModel);

            // Index on kind for GetEventsByKindAsync queries
            var kindIndex = Builders<EventDocument>.IndexKeys
                .Ascending(e => e.Kind);

            var kindIndexModel = new CreateIndexModel<EventDocument>(kindIndex);

            await collection.Indexes.CreateOneAsync(kindIndexModel);
        }
    }

    public async Task<IEnumerable<EventEnvelope>> GetEventEnvelopesAsync<TId>(
        TId aggregateId,
        string aggregateType,
        CancellationToken cancellationToken = default) where TId : notnull
    {
        var collection = GetEventCollection(aggregateType);
        var aggregateIdStr = aggregateId.ToString()!;

        var filter = Builders<EventDocument>.Filter.Eq(e => e.AggregateId, aggregateIdStr);

        var documents = await collection
            .Find(filter)
            .SortBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return documents.Select(ConvertToEnvelope).ToList();
    }

    public async Task<IEnumerable<EventEnvelope>> GetAllEventEnvelopesAsync(
        string aggregateType,
        CancellationToken cancellationToken = default)
    {
        var collection = GetEventCollection(aggregateType);

        var documents = await collection
            .Find(Builders<EventDocument>.Filter.Empty)
            .SortBy(e => e.Timestamp)
            .ThenBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return documents.Select(ConvertToEnvelope).ToList();
    }

    public async Task<IEnumerable<EventEnvelope>> GetAllEventEnvelopesAsync(
        string aggregateType,
        DateTimeOffset fromTimestamp,
        CancellationToken cancellationToken = default)
    {
        var collection = GetEventCollection(aggregateType);

        var filter = Builders<EventDocument>.Filter.Gte(e => e.Timestamp, fromTimestamp);

        var documents = await collection
            .Find(filter)
            .SortBy(e => e.Timestamp)
            .ThenBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return documents.Select(ConvertToEnvelope).ToList();
    }

    public async Task<IEnumerable<EventEnvelope>> GetEventEnvelopesByKindAsync(
        string aggregateType,
        string kind,
        CancellationToken cancellationToken = default)
    {
        var collection = GetEventCollection(aggregateType);

        var filter = Builders<EventDocument>.Filter.Eq(e => e.Kind, kind);

        var documents = await collection
            .Find(filter)
            .SortBy(e => e.Timestamp)
            .ThenBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return documents.Select(ConvertToEnvelope).ToList();
    }

    public async Task<IEnumerable<EventEnvelope>> GetEventEnvelopesByKindsAsync(
        string aggregateType,
        IEnumerable<string> kinds,
        CancellationToken cancellationToken = default)
    {
        var collection = GetEventCollection(aggregateType);

        var kindsList = kinds.ToList();
        var filter = Builders<EventDocument>.Filter.In(e => e.Kind, kindsList);

        var documents = await collection
            .Find(filter)
            .SortBy(e => e.Timestamp)
            .ThenBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return documents.Select(ConvertToEnvelope).ToList();
    }

    public async Task<IEnumerable<string>> GetAllAggregateIdsAsync(
        string aggregateType,
        CancellationToken cancellationToken = default)
    {
        var collection = GetEventCollection(aggregateType);

        // Get distinct aggregate IDs
        var aggregateIds = await collection
            .Distinct(e => e.AggregateId, Builders<EventDocument>.Filter.Empty)
            .ToListAsync(cancellationToken);

        return aggregateIds;
    }

    public async Task<PagedResult<string>> GetAggregateIdsPaginatedAsync(
        string aggregateType,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 1000) pageSize = 1000; // Limite max pour éviter les abus

        var collection = GetEventCollection(aggregateType);

        // Récupérer tous les IDs distincts d'abord
        var allDistinctIds = await collection
            .Distinct(e => e.AggregateId, Builders<EventDocument>.Filter.Empty)
            .ToListAsync(cancellationToken);

        var totalCount = allDistinctIds.Count;

        if (totalCount == 0)
        {
            return PagedResult<string>.Empty(pageNumber, pageSize);
        }

        // Appliquer la pagination sur la liste
        var paginatedIds = allDistinctIds
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<string>(paginatedIds, pageNumber, pageSize, totalCount);
    }

    public async Task<AppendEventsResult> AppendEventsWithResultAsync<TId>(
        TId aggregateId,
        string aggregateType,
        IEnumerable<IEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken = default) where TId : notnull
    {
        if (string.IsNullOrEmpty(aggregateType))
            throw new ArgumentException("Aggregate type cannot be null or empty", nameof(aggregateType));

        if (events == null)
            throw new ArgumentNullException(nameof(events));

        var collection = GetEventCollection(aggregateType);
        var eventsList = events.ToList();

        if (!eventsList.Any())
        {
            return AppendEventsResult.Empty(expectedVersion);
        }

        // Check for concurrency conflicts
        var aggregateIdStr = aggregateId.ToString()!;
        var currentVersion = await GetCurrentVersionAsync(collection, aggregateIdStr, cancellationToken);

        if (currentVersion != expectedVersion)
        {
            throw new ConcurrencyException(aggregateId, expectedVersion, currentVersion);
        }

        // Create event documents and collect event IDs
        var documents = new List<EventDocument>();
        var eventIds = new List<Guid>();
        var version = expectedVersion;

        foreach (var @event in eventsList)
        {
            if (@event == null)
                throw new ArgumentException("Event collection contains null event", nameof(events));

            version++;
            var document = new EventDocument
            {
                AggregateId = aggregateIdStr,
                AggregateType = aggregateType,
                Version = version,
                EventType = @event.EventType ?? throw new InvalidOperationException($"Event {nameof(@event.EventType)} cannot be null"),
                Kind = @event.Kind ?? throw new InvalidOperationException($"Event {nameof(@event.Kind)} cannot be null"),
                EventId = @event.EventId,
                Timestamp = @event.Timestamp,
                Data = _serializer.Serialize(@event)
            };
            documents.Add(document);
            eventIds.Add(@event.EventId);
        }

        // Insert all events atomically
        try
        {
            await collection.InsertManyAsync(documents, cancellationToken: cancellationToken);
            return new AppendEventsResult(eventIds, version);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Concurrency conflict detected
            throw new ConcurrencyException(
                $"Concurrency conflict detected when appending events for aggregate '{aggregateId}'", ex);
        }
    }

    public async Task<Guid> AppendEventAsync<TId>(
        TId aggregateId,
        string aggregateType,
        IEvent @event,
        int expectedVersion,
        CancellationToken cancellationToken = default) where TId : notnull
    {
        if (string.IsNullOrEmpty(aggregateType))
            throw new ArgumentException("Aggregate type cannot be null or empty", nameof(aggregateType));

        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        var collection = GetEventCollection(aggregateType);

        // Check for concurrency conflicts
        var aggregateIdStr = aggregateId.ToString()!;
        var currentVersion = await GetCurrentVersionAsync(collection, aggregateIdStr, cancellationToken);

        if (currentVersion != expectedVersion)
        {
            throw new ConcurrencyException(aggregateId, expectedVersion, currentVersion);
        }

        // Create event document
        var newVersion = expectedVersion + 1;
        var document = new EventDocument
        {
            AggregateId = aggregateIdStr,
            AggregateType = aggregateType,
            Version = newVersion,
            EventType = @event.EventType ?? throw new InvalidOperationException($"Event {nameof(@event.EventType)} cannot be null"),
            Kind = @event.Kind ?? throw new InvalidOperationException($"Event {nameof(@event.Kind)} cannot be null"),
            EventId = @event.EventId,
            Timestamp = @event.Timestamp,
            Data = _serializer.Serialize(@event)
        };

        // Insert the event
        try
        {
            await collection.InsertOneAsync(document, cancellationToken: cancellationToken);
            return @event.EventId;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Concurrency conflict detected
            throw new ConcurrencyException(
                $"Concurrency conflict detected when appending event for aggregate '{aggregateId}'", ex);
        }
    }

    /// <summary>
    /// Deserializes an event from storage and applies any registered upcasters
    /// </summary>
    private IEvent DeserializeAndUpcast(string eventType, string eventData)
    {
        var @event = _serializer.Deserialize(eventType, eventData);

        // Apply upcasting if registry is available
        if (_upcasterRegistry != null)
        {
            @event = _upcasterRegistry.UpcastToLatest(@event);
        }

        return @event;
    }

    private static EventEnvelope ConvertToEnvelope(EventDocument doc)
    {
        // Parse the JSON data to extract only event-specific properties
        // Exclude IEvent properties (eventId, eventType, timestamp, kind) to avoid duplication
        var jsonData = JsonSerializer.Deserialize<Dictionary<string, object>>(doc.Data);

        // Remove metadata properties that are already in the envelope
        var dataToReturn = new Dictionary<string, object>();
        if (jsonData != null)
        {
            foreach (var kvp in jsonData)
            {
                // Exclude IEvent metadata properties (case-insensitive comparison)
                var keyLower = kvp.Key.ToLowerInvariant();
                if (keyLower != "eventid" &&
                    keyLower != "eventtype" &&
                    keyLower != "timestamp" &&
                    keyLower != "kind")
                {
                    dataToReturn[kvp.Key] = kvp.Value;
                }
            }
        }

        return new EventEnvelope
        {
            EventId = doc.EventId,
            EventType = doc.EventType,
            Kind = doc.Kind,
            Timestamp = doc.Timestamp,
            Data = dataToReturn
        };
    }
}
