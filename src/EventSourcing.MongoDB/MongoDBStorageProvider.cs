using EventSourcing.Abstractions;
using MongoDB.Driver;

namespace EventSourcing.MongoDB;

/// <summary>
/// MongoDB implementation of the storage provider.
/// </summary>
public class MongoDBStorageProvider : IEventSourcingStorageProvider
{
    private readonly IMongoDatabase _database;
    private MongoEventStore? _eventStore;
    private MongoSnapshotStore? _snapshotStore;

    public MongoDBStorageProvider(string connectionString, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty", nameof(databaseName));

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public MongoDBStorageProvider(IMongoDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public IEventStore CreateEventStore()
    {
        _eventStore ??= new MongoEventStore(_database);
        return _eventStore;
    }

    public ISnapshotStore CreateSnapshotStore()
    {
        _snapshotStore ??= new MongoSnapshotStore(_database);
        return _snapshotStore;
    }

    public async Task InitializeAsync(IEnumerable<string> aggregateTypes, CancellationToken cancellationToken = default)
    {
        var eventStore = _eventStore ?? new MongoEventStore(_database);
        var snapshotStore = _snapshotStore ?? new MongoSnapshotStore(_database);

        var types = aggregateTypes.ToArray();

        // Create indexes for event store
        await eventStore.EnsureIndexesAsync(types);

        // Create indexes for snapshot store
        await snapshotStore.EnsureIndexesAsync(types);
    }

    public void ValidateConfiguration()
    {
        if (_database == null)
            throw new InvalidOperationException("MongoDB database is not configured");
    }
}
