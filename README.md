# Event Sourcing for .NET

A lightweight, MongoDB-backed event sourcing library for .NET 9+ that makes it easy to implement event sourcing patterns in your applications.

## Features

- **Easy Integration** - Simple NuGet package with minimal configuration
- **MongoDB Native** - Optimized for MongoDB with proper indexing
- **Snapshot Support** - Configurable snapshots to optimize performance
- **Event Kinds** - Auto-generated event categorization for filtering
- **Type Safe** - Strongly typed aggregates and events
- **Concurrency Control** - Built-in optimistic concurrency with versioning
- **CQRS Ready** - Query all events for building projections
- **Extensible** - Provider pattern supports multiple databases

## Installation

```bash
dotnet add package EventSourcing.MongoDB
```

## Quick Start

### 1. Configure Services

```csharp
using EventSourcing.MongoDB;

var builder = WebApplication.CreateBuilder(args);

// Add event sourcing with MongoDB
builder.Services.AddEventSourcing(config =>
{
    config.UseMongoDB("mongodb://localhost:27017", "eventstore")
          .RegisterEventsFromAssembly(typeof(Program).Assembly) // IMPORTANT: Register event types
          .InitializeMongoDB("UserAggregate"); // Initialize indexes

    config.SnapshotEvery(10); // Optional: snapshot every 10 events
});

var app = builder.Build();
app.Run();
```

### 2. Define Events

Events are immutable records that represent state changes:

```csharp
using EventSourcing.Core;

public record UserCreatedEvent(Guid UserId, string Name, string Email) : DomainEvent;

public record UserRenamedEvent(string NewName) : DomainEvent;

public record UserEmailChangedEvent(string NewEmail) : DomainEvent;
```

### 3. Create an Aggregate

Aggregates maintain state and apply events:

```csharp
using EventSourcing.Core;

public class UserAggregate : Aggregate<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    // Required parameterless constructor
    public UserAggregate() { }

    // Factory method for creating new users
    public static UserAggregate Create(Guid id, string name, string email)
    {
        var user = new UserAggregate();
        user.RaiseDomainEvent(new UserCreatedEvent(id, name, email));
        return user;
    }

    // Business methods
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty", nameof(newName));

        RaiseDomainEvent(new UserRenamedEvent(newName));
    }

    public void ChangeEmail(string newEmail)
    {
        if (string.IsNullOrWhiteSpace(newEmail))
            throw new ArgumentException("Email cannot be empty", nameof(newEmail));

        RaiseDomainEvent(new UserEmailChangedEvent(newEmail));
    }

    // Event handlers - must be protected or public
    protected override void When(object @event)
    {
        switch (@event)
        {
            case UserCreatedEvent e:
                Id = e.UserId;
                Name = e.Name;
                Email = e.Email;
                IsActive = true;
                break;

            case UserRenamedEvent e:
                Name = e.NewName;
                break;

            case UserEmailChangedEvent e:
                Email = e.NewEmail;
                break;
        }
    }
}
```

### 4. Use in Your Application

```csharp
using EventSourcing.Abstractions;

public class UserService
{
    private readonly IAggregateRepository<UserAggregate, Guid> _repository;

    public UserService(IAggregateRepository<UserAggregate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<Guid> CreateUserAsync(string name, string email)
    {
        var userId = Guid.NewGuid();
        var user = UserAggregate.Create(userId, name, email);

        await _repository.SaveAsync(user);
        return userId;
    }

    public async Task RenameUserAsync(Guid userId, string newName)
    {
        var user = await _repository.GetByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException($"User {userId} not found");

        user.Rename(newName);
        await _repository.SaveAsync(user);
    }

    public async Task<UserAggregate?> GetUserAsync(Guid userId)
    {
        return await _repository.GetByIdAsync(userId);
    }
}
```

## Core Concepts

### Event Sourcing

Instead of storing just the current state, event sourcing stores **all state changes as immutable events**. The current state is reconstructed by replaying all events for an aggregate.

**Benefits:**
- Complete audit trail
- Time travel (reconstruct state at any point)
- Event replay for debugging
- Easy to build projections and read models

### Aggregates

An aggregate is a cluster of domain objects that can be treated as a single unit. Each aggregate has:
- **Identity** - Unique identifier (Guid, int, string, etc.)
- **Version** - Used for optimistic concurrency control
- **Events** - Uncommitted domain events that represent state changes
- **State** - Reconstructed by applying events

### Events

Events are immutable facts that represent something that happened:
- Named in past tense (UserCreated, OrderPlaced)
- Contain all data needed to reconstruct state
- Have unique EventId and Timestamp
- Automatically generate a "Kind" for categorization

### Event Kinds

Events automatically generate a "kind" field for categorization:

```csharp
public record UserCreatedEvent(Guid UserId, string Name, string Email) : DomainEvent;
// Auto-generates Kind: "user.created"

public record OrderPlacedEvent(Guid OrderId, decimal Amount) : DomainEvent;
// Auto-generates Kind: "order.placed"
```

You can also specify a custom kind:

```csharp
public record CustomEvent(string Data) : DomainEvent("custom.category");
```

### Snapshots

Snapshots are point-in-time captures of aggregate state that optimize performance:

```csharp
builder.Services.AddEventSourcing(options =>
{
    options.UseMongoDB("mongodb://localhost:27017", "eventstore");
    options.SnapshotEvery(10); // Snapshot every 10 events
});
```

**How it works:**
1. When saving, if (version % snapshotFrequency == 0), a snapshot is created
2. When loading, the latest snapshot is retrieved + subsequent events
3. State is reconstructed from snapshot, then events are replayed

**Performance:**
- No snapshots: Replay 1000 events
- Snapshot every 10: Replay 10 events max
- Snapshot every 1: No replay, but many writes (not recommended)

**Recommended:** Snapshot every 10-50 events depending on your aggregate complexity.

### Concurrency Control

The library uses optimistic concurrency control with version numbers:

```csharp
// Thread 1
var user = await repository.GetByIdAsync(userId); // Version = 5
user.Rename("Alice");
await repository.SaveAsync(user); // Version = 6 ✓

// Thread 2 (concurrent)
var user = await repository.GetByIdAsync(userId); // Version = 5
user.ChangeEmail("new@email.com");
await repository.SaveAsync(user); // ConcurrencyException! Expected 5, got 6
```

Handle with retry logic or merge strategies.

## Advanced Usage

### Querying Events

Get all events for a specific aggregate:

```csharp
var events = await eventStore.GetEventsAsync(userId, "UserAggregate");
```

Get all events across all aggregates (for projections):

```csharp
// All events
var allEvents = await eventStore.GetAllEventsAsync("UserAggregate");

// Events since a timestamp (incremental processing)
var recentEvents = await eventStore.GetAllEventsAsync(
    "UserAggregate",
    DateTimeOffset.UtcNow.AddDays(-7)
);

// Filter by event kind
var createdEvents = await eventStore.GetEventsByKindAsync(
    "UserAggregate",
    "user.created"
);

// Filter by multiple kinds
var events = await eventStore.GetEventsByKindsAsync(
    "UserAggregate",
    new[] { "user.created", "user.renamed" }
);
```

### Building Projections (CQRS)

Use event queries to build read models:

```csharp
public class UserProjectionBuilder
{
    private readonly IEventStore _eventStore;
    private readonly IUserReadModelRepository _readModelRepo;

    public async Task RebuildProjectionAsync()
    {
        var events = await _eventStore.GetAllEventsAsync("UserAggregate");

        foreach (var evt in events)
        {
            switch (evt)
            {
                case UserCreatedEvent e:
                    await _readModelRepo.InsertAsync(new UserReadModel
                    {
                        Id = e.UserId,
                        Name = e.Name,
                        Email = e.Email
                    });
                    break;

                case UserRenamedEvent e:
                    await _readModelRepo.UpdateNameAsync(e.UserId, e.NewName);
                    break;
            }
        }
    }
}
```

### Custom Aggregate ID Types

Support for any ID type:

```csharp
// String IDs
public class ProductAggregate : Aggregate<string> { }

// Int IDs
public class OrderAggregate : Aggregate<int> { }

// Custom types (must override ToString())
public record CustomId(string Prefix, int Number);
public class CustomAggregate : Aggregate<CustomId> { }
```

### Event Handlers Outside Aggregate

Implement `IEventHandler<TEvent>` for side effects:

```csharp
public class SendWelcomeEmailHandler : IEventHandler<UserCreatedEvent>
{
    private readonly IEmailService _emailService;

    public async Task HandleAsync(UserCreatedEvent @event)
    {
        await _emailService.SendWelcomeEmailAsync(@event.Email, @event.Name);
    }
}
```

Register handlers:

```csharp
builder.Services.AddEventSourcing(options =>
{
    options.UseMongoDB("mongodb://localhost:27017", "eventstore");
    options.AddEventHandler<SendWelcomeEmailHandler>();
});
```

## MongoDB Collections

For each aggregate type, the following collections are created:

```
{aggregateType}_events      - All events (append-only)
{aggregateType}_snapshots   - Periodic snapshots
```

Example for `UserAggregate`:
```
user_events
user_snapshots
```

### Indexes

The library automatically creates these indexes:

**Events Collection:**
- `{ aggregateId: 1, version: 1 }` (unique) - Fast aggregate loading
- `{ timestamp: 1 }` - Time-based queries
- `{ kind: 1 }` - Event kind filtering

**Snapshots Collection:**
- `{ aggregateId: 1, version: -1 }` - Fast snapshot retrieval

## Testing

The library is designed to be testable:

```csharp
[Fact]
public void User_CanBeRenamed()
{
    // Arrange
    var user = UserAggregate.Create(Guid.NewGuid(), "John", "john@example.com");

    // Act
    user.Rename("Jane");

    // Assert
    user.Name.Should().Be("Jane");
    user.GetUncommittedEvents().Should().HaveCount(2); // Created + Renamed
}

[Fact]
public async Task Repository_HandlesOptimisticConcurrency()
{
    // Arrange
    var user = UserAggregate.Create(Guid.NewGuid(), "John", "john@example.com");
    await repository.SaveAsync(user);

    // Act - Simulate concurrent modification
    var user1 = await repository.GetByIdAsync(user.Id);
    var user2 = await repository.GetByIdAsync(user.Id);

    user1.Rename("Alice");
    await repository.SaveAsync(user1);

    user2.Rename("Bob");

    // Assert
    await Assert.ThrowsAsync<ConcurrencyException>(() =>
        repository.SaveAsync(user2)
    );
}
```

## API Example

See the `EventSourcing.Example.Api` project for a complete ASP.NET Core example with:
- REST endpoints for user management
- Event history endpoints
- Event filtering by kind
- Swagger documentation

Run the example:

```bash
cd examples/EventSourcing.Example.Api
dotnet run
```

Then visit `http://localhost:5000/swagger` to explore the API.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                   Application Layer                  │
│  (Controllers, Services, Command/Query Handlers)    │
└─────────────────┬───────────────────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────┐
│              Domain Layer (Aggregates)              │
│  • UserAggregate, OrderAggregate, etc.              │
│  • Business logic & invariants                      │
│  • Raise domain events                              │
└─────────────────┬───────────────────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────┐
│         Repository (IAggregateRepository)           │
│  • Load aggregates (snapshots + events)             │
│  • Save aggregates (append events + snapshots)      │
│  • Optimistic concurrency checks                    │
└──────────┬─────────────────────────────────┬────────┘
           │                                 │
┌──────────▼─────────┐           ┌───────────▼────────┐
│  IEventStore       │           │  ISnapshotStore    │
│  • Append events   │           │  • Save snapshots  │
│  • Query events    │           │  • Load snapshots  │
│  • MongoDB impl    │           │  • MongoDB impl    │
└────────────────────┘           └────────────────────┘
```

## Best Practices

### 1. Keep Events Small and Focused

```csharp
// Good - Focused events
public record UserRenamedEvent(string NewName) : DomainEvent;
public record UserEmailChangedEvent(string NewEmail) : DomainEvent;

// Bad - Kitchen sink event
public record UserUpdatedEvent(string? Name, string? Email, bool? Active) : DomainEvent;
```

### 2. Name Events in Past Tense

```csharp
// Good
public record UserCreatedEvent(...) : DomainEvent;
public record OrderPlacedEvent(...) : DomainEvent;

// Bad
public record CreateUserEvent(...) : DomainEvent;
public record PlaceOrderEvent(...) : DomainEvent;
```

### 3. Don't Delete or Modify Events

Events are immutable facts. Never modify stored events. For schema changes, use event versioning:

```csharp
public record UserCreatedEventV2(
    Guid UserId,
    string Name,
    string Email,
    string PhoneNumber  // New field
) : DomainEvent;
```

### 4. Use Snapshots Wisely

- Default: `SnapshotEvery(10)` for most aggregates
- High-frequency: `SnapshotEvery(50)` if events are cheap to replay
- Low-frequency: `SnapshotEvery(5)` if events are expensive

### 5. Build Projections for Queries

Don't query aggregates directly. Use CQRS with projections:

```csharp
// Bad - Loading all aggregates to find active users
var users = await repository.GetAllAsync();
var activeUsers = users.Where(u => u.IsActive);

// Good - Query optimized read model
var activeUsers = await userReadModel.GetActiveUsersAsync();
```

### 6. Handle Concurrency Thoughtfully

```csharp
public async Task RenameUserWithRetryAsync(Guid userId, string newName)
{
    const int maxRetries = 3;

    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            var user = await _repository.GetByIdAsync(userId);
            user.Rename(newName);
            await _repository.SaveAsync(user);
            return;
        }
        catch (ConcurrencyException) when (i < maxRetries - 1)
        {
            // Retry with latest version
            await Task.Delay(100 * (i + 1)); // Exponential backoff
        }
    }

    throw new InvalidOperationException("Failed to update user after retries");
}
```

## Performance Considerations

### Event Store Performance

- **Writes**: O(1) - Append-only, very fast
- **Reads**: O(log n) with indexes
- **Snapshots**: O(1) with proper indexing

### Optimization Tips

1. **Use Snapshots** - Reduces event replay overhead
2. **Index Properly** - Call `EnsureIndexesAsync()` on startup
3. **Batch Operations** - Use MongoDB transactions for multiple aggregates
4. **Async All The Way** - Use async/await throughout
5. **Connection Pooling** - MongoDB driver handles this automatically

## Troubleshooting

### "Event type 'XxxEvent' is not registered"

**Error:**
```
System.InvalidOperationException: Event type 'UserCreatedEvent' is not registered.
Make sure to register all event types during application startup.
```

**Cause:** Event types must be registered for deserialization from MongoDB.

**Solution:** Add `.RegisterEventsFromAssembly()` to your configuration:

```csharp
builder.Services.AddEventSourcing(config =>
{
    config.UseMongoDB("mongodb://localhost:27017", "eventstore")
          .RegisterEventsFromAssembly(typeof(Program).Assembly) // Register all events
          .InitializeMongoDB("UserAggregate");
});
```

Or register specific event types:

```csharp
config.RegisterEventTypes(
    typeof(UserCreatedEvent),
    typeof(UserRenamedEvent),
    typeof(UserEmailChangedEvent)
);
```

### "Concurrency conflicts are frequent"

Increase retry logic or redesign to reduce contention:
- Split large aggregates into smaller ones
- Use eventual consistency between aggregates
- Consider using sagas for distributed transactions

### "Event replay is slow"

Check snapshot configuration:
```csharp
options.SnapshotEvery(10); // Increase snapshot frequency
```

Verify indexes are created:
```csharp
await mongoStore.EnsureIndexesAsync("UserAggregate");
```

### "MongoDB connection issues"

Verify connection string and network access:
```csharp
builder.Services.AddEventSourcing(options =>
{
    options.UseMongoDB(
        "mongodb://localhost:27017",
        "eventstore"
    );
});
```

## Roadmap

- [ ] SQL Server provider
- [ ] PostgreSQL provider
- [ ] Event versioning and upcasting
- [ ] Saga support for long-running processes
- [ ] Event subscriptions and notifications
- [ ] Projection framework
- [ ] Migration tools

## Contributing

Contributions are welcome! Please submit issues and pull requests on GitHub.

## License

MIT License - see LICENSE file for details

## Support

- **Issues**: [GitHub Issues](https://github.com/Dyshay/EventSourcing/issues)