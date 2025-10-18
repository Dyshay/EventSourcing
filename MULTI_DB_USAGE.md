# Multi-Database Support - Usage Guide

Le package Event Sourcing supporte maintenant plusieurs bases de données via un système de providers pluggables.

## Utilisation avec MongoDB (Built-in)

```csharp
using EventSourcing.MongoDB;

// Dans Program.cs ou Startup.cs
services.AddEventSourcing(builder => {
    // Configurer MongoDB comme storage provider
    builder.UseMongoDB("mongodb://localhost:27017", "EventSourcingDB")
           .InitializeMongoDB("User", "Order", "Product"); // Initialise les indexes

    // Configuration des snapshots
    builder.SnapshotEvery(10);

    // Optionnel: Ajouter des projections
    builder.AddProjection<UserListProjection>();

    // Optionnel: Publisher externe
    builder.AddEventPublisher<RabbitMQPublisher>();
});
```

## Créer un provider PostgreSQL

### 1. Créer le projet

```bash
mkdir src/EventSourcing.PostgreSQL
cd src/EventSourcing.PostgreSQL
dotnet new classlib
dotnet add reference ../EventSourcing.Core
dotnet add package Npgsql
```

### 2. Implémenter PostgreSQLStorageProvider

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
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var aggregateType in aggregateTypes)
        {
            // Créer les tables events et snapshots
            await CreateTablesAsync(connection, aggregateType, cancellationToken);
        }
    }

    public void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("PostgreSQL connection string is required");
    }

    private async Task CreateTablesAsync(NpgsqlConnection connection, string aggregateType, CancellationToken cancellationToken)
    {
        var eventsTable = $"{aggregateType.ToLowerInvariant()}_events";
        var snapshotsTable = $"{aggregateType.ToLowerInvariant()}_snapshots";

        // Table des événements
        var createEventsTableSql = $@"
            CREATE TABLE IF NOT EXISTS {eventsTable} (
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
            CREATE INDEX IF NOT EXISTS idx_{eventsTable}_aggregate ON {eventsTable}(aggregate_id);
        ";

        // Table des snapshots
        var createSnapshotsTableSql = $@"
            CREATE TABLE IF NOT EXISTS {snapshotsTable} (
                id BIGSERIAL PRIMARY KEY,
                aggregate_id TEXT NOT NULL,
                aggregate_type TEXT NOT NULL,
                version INTEGER NOT NULL,
                timestamp TIMESTAMPTZ NOT NULL,
                data JSONB NOT NULL,
                UNIQUE(aggregate_id, aggregate_type)
            );
            CREATE INDEX IF NOT EXISTS idx_{snapshotsTable}_aggregate ON {snapshotsTable}(aggregate_id);
        ";

        using (var cmd = new NpgsqlCommand(createEventsTableSql, connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        using (var cmd = new NpgsqlCommand(createSnapshotsTableSql, connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
```

### 3. Créer les extensions pour faciliter l'utilisation

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
        builder.Services.AddSingleton<IEventSourcingStorageProvider>(provider);
        return builder;
    }

    public static EventSourcingBuilder InitializePostgreSQL(
        this EventSourcingBuilder builder,
        params string[] aggregateTypes)
    {
        builder.Services.AddHostedService(sp =>
        {
            var provider = sp.GetRequiredService<IEventSourcingStorageProvider>();
            return new PostgreSQLInitializationService(provider, aggregateTypes);
        });
        return builder;
    }
}
```

### 4. Utiliser le provider PostgreSQL

```csharp
using EventSourcing.PostgreSQL;

services.AddEventSourcing(builder => {
    builder.UsePostgreSQL("Host=localhost;Database=eventsourcing;Username=postgres;Password=***")
           .InitializePostgreSQL("User", "Order");

    builder.SnapshotEvery(10);
});
```

## Autres providers possibles

### SQL Server

```csharp
builder.UseSqlServer("Server=localhost;Database=EventSourcing;Trusted_Connection=True;");
```

### CosmosDB

```csharp
builder.UseCosmosDB(
    endpoint: "https://myaccount.documents.azure.com:443/",
    authKey: "mykey==",
    databaseName: "EventSourcingDB"
);
```

### EventStoreDB

```csharp
builder.UseEventStoreDB("esdb://localhost:2113?tls=false");
```

### Redis (pour event streaming)

```csharp
builder.UseRedis("localhost:6379");
```

## Architecture du Provider

Chaque provider doit implémenter `IEventSourcingStorageProvider` avec :

1. **CreateEventStore()** - Retourne une implémentation de `IEventStore`
2. **CreateSnapshotStore()** - Retourne une implémentation de `ISnapshotStore`
3. **InitializeAsync()** - Initialise le storage (tables, collections, indexes)
4. **ValidateConfiguration()** - Valide la configuration au startup

## Comparaison des providers

| Feature | MongoDB | PostgreSQL | SQL Server | CosmosDB |
|---------|---------|------------|------------|----------|
| Collections dynamiques | ✅ | ❌ (tables fixes) | ❌ (tables fixes) | ✅ |
| Transactions | ✅ | ✅ | ✅ | ✅ |
| JSON natif | ✅ (BSON) | ✅ (JSONB) | ⚠️ (NVARCHAR) | ✅ |
| Scalabilité | ✅✅ | ✅ | ✅ | ✅✅✅ |
| Coût | Moyen | Faible | Moyen | Élevé |
| Setup complexité | Faible | Moyen | Moyen | Moyen |

## Migration entre providers

Pour migrer d'un provider à un autre :

```csharp
// 1. Exporter les événements depuis l'ancien provider
var oldProvider = new MongoDBStorageProvider("...", "...");
var eventStore = oldProvider.CreateEventStore();
var events = await eventStore.GetEventsAsync(...);

// 2. Configurer le nouveau provider
var newProvider = new PostgreSQLStorageProvider("...");
await newProvider.InitializeAsync(new[] { "User", "Order" });

// 3. Importer les événements
var newEventStore = newProvider.CreateEventStore();
await newEventStore.AppendEventsAsync(...);
```

Un outil de migration CLI pourrait être créé pour automatiser ce processus.
