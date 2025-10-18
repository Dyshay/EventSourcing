# Creating Custom Storage Providers

This guide explains how to create custom storage providers for different databases.

## Overview

The Event Sourcing library uses a provider-based architecture that allows you to plug in any database as the storage backend. This document shows you how to create your own provider.

## Existing Providers

- **EventSourcing.MongoDB** - MongoDB implementation (built-in, production-ready)

## Creating a New Provider

### Step 1: Create the Project

```bash
dotnet new classlib -n EventSourcing.PostgreSQL
cd EventSourcing.PostgreSQL
dotnet add reference ../EventSourcing.Abstractions
dotnet add package Npgsql
```

### Step 2: Implement the Storage Provider Interface

Create a class that implements `IEventSourcingStorageProvider`:

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

    public async Task InitializeAsync(
        IEnumerable<string> aggregateTypes,
        CancellationToken cancellationToken = default)
    {
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

    private async Task CreateEventTableAsync(
        NpgsqlConnection connection,
        string aggregateType,
        CancellationToken cancellationToken)
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
                kind TEXT NOT NULL,
                timestamp TIMESTAMPTZ NOT NULL,
                data JSONB NOT NULL,
                UNIQUE(aggregate_id, version)
            );
            CREATE INDEX IF NOT EXISTS idx_{tableName}_aggregate_id
                ON {tableName}(aggregate_id);
            CREATE INDEX IF NOT EXISTS idx_{tableName}_kind
                ON {tableName}(kind);
            CREATE INDEX IF NOT EXISTS idx_{tableName}_timestamp
                ON {tableName}(timestamp DESC);
        ";

        using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task CreateSnapshotTableAsync(
        NpgsqlConnection connection,
        string aggregateType,
        CancellationToken cancellationToken)
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
            CREATE INDEX IF NOT EXISTS idx_{tableName}_aggregate
                ON {tableName}(aggregate_id, aggregate_type);
        ";

        using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
```

### Step 3: Implement Event Store

Create an implementation of `IEventStore`:

```csharp
using System.Text.Json;
using EventSourcing.Abstractions;
using Npgsql;

namespace EventSourcing.PostgreSQL;

public class PostgreSQLEventStore : IEventStore
{
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

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
                    (aggregate_id, aggregate_type, version, event_type, event_id, kind, timestamp, data)
                    VALUES (@aggregateId, @aggregateType, @version, @eventType, @eventId, @kind, @timestamp, @data::jsonb)
                ";

                using var command = new NpgsqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("aggregateId", aggregateId.ToString()!);
                command.Parameters.AddWithValue("aggregateType", aggregateType);
                command.Parameters.AddWithValue("version", version);
                command.Parameters.AddWithValue("eventType", @event.EventType);
                command.Parameters.AddWithValue("eventId", @event.EventId);
                command.Parameters.AddWithValue("kind", @event.Kind);
                command.Parameters.AddWithValue("timestamp", @event.Timestamp);
                command.Parameters.AddWithValue("data", JsonSerializer.Serialize(@event, JsonOptions));

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

    public async Task<IEnumerable<IEvent>> GetEventsAsync<TId>(
        TId aggregateId,
        string aggregateType,
        int fromVersion = 0,
        CancellationToken cancellationToken = default) where TId : notnull
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var tableName = $"{aggregateType.ToLowerInvariant()}_events";
        var sql = $@"
            SELECT event_type, event_id, kind, timestamp, data, version
            FROM {tableName}
            WHERE aggregate_id = @aggregateId AND version > @fromVersion
            ORDER BY version ASC
        ";

        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("aggregateId", aggregateId.ToString()!);
        command.Parameters.AddWithValue("fromVersion", fromVersion);

        var events = new List<IEvent>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var eventType = reader.GetString(0);
            var eventId = reader.GetGuid(1);
            var kind = reader.GetString(2);
            var timestamp = reader.GetDateTime(3);
            var data = reader.GetString(4);

            // Deserialize event based on type
            // Implementation depends on your event registration system
            var @event = DeserializeEvent(eventType, data);
            events.Add(@event);
        }

        return events;
    }

    public async Task<IEnumerable<string>> GetAllAggregateIdsAsync(
        string aggregateType,
        CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var tableName = $"{aggregateType.ToLowerInvariant()}_events";
        var sql = $@"
            SELECT DISTINCT aggregate_id
            FROM {tableName}
            ORDER BY aggregate_id
        ";

        using var command = new NpgsqlCommand(sql, connection);

        var aggregateIds = new List<string>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            aggregateIds.Add(reader.GetString(0));
        }

        return aggregateIds;
    }

    // Additional methods: GetEventsByKindAsync, GetEventsSinceAsync, GetEventEnvelopesAsync...
}
```

### Step 4: Implement Snapshot Store

Create an implementation of `ISnapshotStore`:

```csharp
public class PostgreSQLSnapshotStore : ISnapshotStore
{
    private readonly string _connectionString;

    public PostgreSQLSnapshotStore(string connectionString)
    {
        _connectionString = connectionString;
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
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var tableName = $"{aggregateType.ToLowerInvariant()}_snapshots";
        var sql = $@"
            INSERT INTO {tableName}
            (aggregate_id, aggregate_type, version, timestamp, data)
            VALUES (@aggregateId, @aggregateType, @version, @timestamp, @data::jsonb)
            ON CONFLICT (aggregate_id, aggregate_type)
            DO UPDATE SET version = @version, timestamp = @timestamp, data = @data::jsonb
        ";

        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("aggregateId", aggregateId.ToString()!);
        command.Parameters.AddWithValue("aggregateType", aggregateType);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("timestamp", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("data", SerializeAggregate(aggregate));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Additional methods: GetSnapshotAsync, DeleteSnapshotAsync...
}
```

### Step 5: Create Builder Extensions

Make it easy to use your provider:

```csharp
using EventSourcing.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.PostgreSQL;

public static class PostgreSQLExtensions
{
    public static EventSourcingBuilder UsePostgreSQL(
        this EventSourcingBuilder builder,
        string connectionString)
    {
        var provider = new PostgreSQLStorageProvider(connectionString);

        // Register the provider
        builder.Services.AddSingleton<IEventSourcingStorageProvider>(provider);

        // Register stores
        builder.Services.AddSingleton(provider.CreateEventStore());
        builder.Services.AddSingleton(provider.CreateSnapshotStore());

        return builder;
    }

    public static EventSourcingBuilder InitializePostgreSQL(
        this EventSourcingBuilder builder,
        params string[] aggregateTypes)
    {
        builder.AddInitializer(async (provider, ct) =>
        {
            await provider.InitializeAsync(aggregateTypes, ct);
        });

        return builder;
    }
}
```

### Step 6: Usage

```csharp
using EventSourcing.PostgreSQL;

// Startup.cs or Program.cs
services.AddEventSourcing(config => {
    config.UsePostgreSQL("Host=localhost;Database=myapp;Username=user;Password=pass")
          .InitializePostgreSQL("User", "Order", "Product")
          .SnapshotEvery(10);
});
```

## Schema Design Patterns

### Table-per-Aggregate Pattern (Recommended)

```sql
-- user_events table
CREATE TABLE user_events (
    id BIGSERIAL PRIMARY KEY,
    aggregate_id TEXT NOT NULL,
    version INTEGER NOT NULL,
    event_type TEXT NOT NULL,
    event_id UUID NOT NULL,
    kind TEXT NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    data JSONB NOT NULL,
    UNIQUE(aggregate_id, version)
);

-- order_events table
CREATE TABLE order_events (
    -- Same structure
);
```

### Single-Table Pattern

```sql
-- All events in one table
CREATE TABLE events (
    id BIGSERIAL PRIMARY KEY,
    aggregate_id TEXT NOT NULL,
    aggregate_type TEXT NOT NULL,
    version INTEGER NOT NULL,
    event_type TEXT NOT NULL,
    event_id UUID NOT NULL,
    kind TEXT NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    data JSONB NOT NULL,
    UNIQUE(aggregate_id, aggregate_type, version)
);
```

## Provider Comparison

| Feature | MongoDB | PostgreSQL | SQL Server | CosmosDB | EventStoreDB |
|---------|---------|------------|------------|----------|--------------|
| Native JSON | ✅ BSON | ✅ JSONB | ⚠️ NVARCHAR | ✅ JSON | ✅ Binary |
| Transactions | ✅ | ✅ | ✅ | ✅ | ✅ |
| Streams | ❌ | ❌ | ❌ | ❌ | ✅ |
| Projections | Custom | Custom | Custom | Custom | Built-in |
| Indexing | Automatic | Manual | Manual | Automatic | Automatic |
| Scalability | ✅✅ | ✅ | ✅ | ✅✅✅ | ✅✅ |
| Cost | Medium | Low | Medium | High | Low |
| Learning Curve | Low | Medium | Medium | Medium | High |

## Testing Your Provider

Create integration tests:

```csharp
public class PostgreSQLProviderTests : IAsyncLifetime
{
    private PostgreSQLStorageProvider _provider;

    public async Task InitializeAsync()
    {
        _provider = new PostgreSQLStorageProvider("test-connection-string");
        await _provider.InitializeAsync(["TestAggregate"]);
    }

    [Fact]
    public async Task AppendEvents_Should_Store_Events()
    {
        // Arrange
        var eventStore = _provider.CreateEventStore();
        var events = [new TestEvent()];

        // Act
        await eventStore.AppendEventsAsync(
            Guid.NewGuid(),
            "TestAggregate",
            events,
            expectedVersion: 0);

        // Assert
        var storedEvents = await eventStore.GetEventsAsync(...);
        storedEvents.Should().HaveCount(1);
    }

    public async Task DisposeAsync()
    {
        // Cleanup test database
    }
}
```

## Best Practices

1. **Connection Management**: Use connection pooling
2. **Transactions**: Always use transactions for event appends
3. **Indexes**: Create proper indexes on aggregate_id, version, kind, and timestamp
4. **Serialization**: Use efficient JSON serialization (System.Text.Json or Newtonsoft)
5. **Error Handling**: Handle concurrency violations appropriately
6. **Schema Evolution**: Plan for event schema upgrades
7. **Performance**: Batch operations when possible
8. **Monitoring**: Add logging and metrics

## Publishing Your Provider

1. Create a NuGet package
2. Add comprehensive documentation
3. Include examples
4. Publish to nuget.org

```xml
<!-- EventSourcing.PostgreSQL.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <PackageId>EventSourcing.PostgreSQL</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>PostgreSQL storage provider for Event Sourcing library</Description>
    <PackageTags>eventsourcing;postgresql;cqrs</PackageTags>
  </PropertyGroup>
</Project>
```
