# Event Sourcing Storage Providers

This document explains how to create custom storage providers for different databases.

## Existing Providers

- **EventSourcing.MongoDB** - MongoDB implementation (built-in)

## Creating a Custom Provider

To create support for a new database (PostgreSQL, SQL Server, CosmosDB, etc.), follow these steps:

### 1. Create a new project

```bash
dotnet new classlib -n EventSourcing.PostgreSQL
dotnet add EventSourcing.PostgreSQL reference EventSourcing.Core
```

### 2. Implement the Storage Provider

```csharp
using EventSourcing.Abstractions;
using Npgsql;

namespace EventSourcing.PostgreSQL;

public class PostgreSQLStorageProvider : IEventSourcingStorageProvider
{
    private readonly string _connectionString;
    private PostgreSQLEventStore? _eventStore;
    private PostgreSQLSnapshotStore? _snapshotStore;

    public PostgreSQLStorageProvider(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public IEventStore CreateEventStore()
    {
        _eventStore ??= new PostgreSQLEventStore(_connectionString);
        return _eventStore;
    }

    public ISnapshotStore CreateSnapshotStore()
    {
        _snapshotStore ??= new PostgreSQLSnapshotStore(_connectionString);
        return _snapshotStore;
    }

    public async Task InitializeAsync(IEnumerable<string> aggregateTypes, CancellationToken cancellationToken = default)
    {
        // Create tables and indexes for PostgreSQL
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var aggregateType in aggregateTypes)
        {
            await CreateEventTableAsync(connection, aggregateType, cancellationToken);
            await CreateSnapshotTableAsync(connection, aggregateType, cancellationToken);
        }
    }

    public void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("PostgreSQL connection string is not configured");
    }

    private async Task CreateEventTableAsync(NpgsqlConnection connection, string aggregateType, CancellationToken cancellationToken)
    {
        var tableName = $"{aggregateType.ToLowerInvariant()}_events";
        var sql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                id BIGSERIAL PRIMARY KEY,
                aggregate_id TEXT NOT NULL,
                aggregate_type TEXT NOT NULL,
                version INTEGER NOT NULL,
                event_type TEXT NOT NULL,
                event_id UUID NOT NULL,
                timestamp TIMESTAMPTZ NOT NULL,
                data JSONB NOT NULL,
                UNIQUE(aggregate_id, version)
            );
            CREATE INDEX IF NOT EXISTS idx_{tableName}_aggregate_id ON {tableName}(aggregate_id);
            CREATE INDEX IF NOT EXISTS idx_{tableName}_aggregate_version ON {tableName}(aggregate_id, version);
        ";

        using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task CreateSnapshotTableAsync(NpgsqlConnection connection, string aggregateType, CancellationToken cancellationToken)
    {
        var tableName = $"{aggregateType.ToLowerInvariant()}_snapshots";
        var sql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                id BIGSERIAL PRIMARY KEY,
                aggregate_id TEXT NOT NULL,
                aggregate_type TEXT NOT NULL,
                version INTEGER NOT NULL,
                timestamp TIMESTAMPTZ NOT NULL,
                data JSONB NOT NULL,
                UNIQUE(aggregate_id, aggregate_type)
            );
            CREATE INDEX IF NOT EXISTS idx_{tableName}_aggregate ON {tableName}(aggregate_id, aggregate_type);
        ";

        using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
```

### 3. Implement Event Store

```csharp
public class PostgreSQLEventStore : IEventStore
{
    private readonly string _connectionString;

    public PostgreSQLEventStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task AppendEventsAsync<TId>(
        TId aggregateId,
        string aggregateType,
        IEnumerable<IEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken = default) where TId : notnull
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var tableName = $"{aggregateType.ToLowerInvariant()}_events";
            var version = expectedVersion;

            foreach (var @event in events)
            {
                version++;
                var sql = $@"
                    INSERT INTO {tableName}
                    (aggregate_id, aggregate_type, version, event_type, event_id, timestamp, data)
                    VALUES (@aggregateId, @aggregateType, @version, @eventType, @eventId, @timestamp, @data::jsonb)
                ";

                using var command = new NpgsqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("aggregateId", aggregateId.ToString());
                command.Parameters.AddWithValue("aggregateType", aggregateType);
                command.Parameters.AddWithValue("version", version);
                command.Parameters.AddWithValue("eventType", @event.EventType);
                command.Parameters.AddWithValue("eventId", @event.EventId);
                command.Parameters.AddWithValue("timestamp", @event.Timestamp);
                command.Parameters.AddWithValue("data", SerializeEvent(@event));

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // Implement other methods...
}
```

### 4. Usage with Configuration

```csharp
// Startup.cs or Program.cs
services.AddEventSourcing(builder => {
    // Use PostgreSQL instead of MongoDB
    builder.UsePostgreSQL(connectionString);

    // Or use SQL Server
    // builder.UseSqlServer(connectionString);

    // Or use CosmosDB
    // builder.UseCosmosDB(endpoint, authKey, databaseName);

    builder.SnapshotEvery(10);
    builder.AddProjection<UserListProjection>();
});
```

## Provider Comparison

| Provider | Collections/Tables | Indexing | Transactions | JSON Storage |
|----------|-------------------|----------|--------------|--------------|
| MongoDB | Native collections | Automatic | ✅ | Native BSON |
| PostgreSQL | Tables | Manual | ✅ | JSONB column |
| SQL Server | Tables | Manual | ✅ | NVARCHAR(MAX) |
| CosmosDB | Containers | Automatic | ✅ | Native JSON |
| EventStoreDB | Streams | Automatic | ✅ | Native |

## Schema Patterns

### MongoDB (Current)
```
Collection: user_events
{
  "_id": ObjectId,
  "aggregateId": "guid-string",
  "aggregateType": "User",
  "version": 1,
  "eventType": "UserCreatedEvent",
  "eventId": "guid",
  "timestamp": ISODate,
  "data": { ... }
}
```

### PostgreSQL (Example)
```sql
Table: user_events
id | aggregate_id | aggregate_type | version | event_type | event_id | timestamp | data (JSONB)
```

### SQL Server (Example)
```sql
Table: user_events
Id | AggregateId | AggregateType | Version | EventType | EventId | Timestamp | Data (NVARCHAR(MAX))
```

## Extension Methods for Builders

Each provider should provide extension methods for the builder:

```csharp
public static class PostgreSQLExtensions
{
    public static EventSourcingBuilder UsePostgreSQL(
        this EventSourcingBuilder builder,
        string connectionString)
    {
        var provider = new PostgreSQLStorageProvider(connectionString);
        builder.Services.AddSingleton<IEventSourcingStorageProvider>(provider);

        // Register stores
        builder.Services.AddSingleton(provider.CreateEventStore());
        builder.Services.AddSingleton(provider.CreateSnapshotStore());

        return builder;
    }
}
```
