# Event Sourcing for .NET

[![CI Build and Test](https://github.com/Dyshay/EventSourcing/actions/workflows/ci.yml/badge.svg)](https://github.com/Dyshay/EventSourcing/actions/workflows/ci.yml)
[![Code Coverage](https://github.com/Dyshay/EventSourcing/actions/workflows/code-coverage.yml/badge.svg)](https://github.com/Dyshay/EventSourcing/actions/workflows/code-coverage.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download)

A lightweight, production-ready event sourcing library for .NET 9+ with MongoDB backend. Build CQRS applications with confidence using battle-tested patterns and comprehensive test coverage.

## Why Event Sourcing?

Event sourcing captures **all changes to application state** as a sequence of immutable events, providing:

- ✅ **Complete Audit Trail** - Every state change is recorded
- ✅ **Time Travel** - Reconstruct state at any point in time
- ✅ **Event Replay** - Rebuild read models from events
- ✅ **Business Intelligence** - Rich event history for analytics
- ✅ **CQRS Ready** - Natural fit for Command Query Responsibility Segregation

## Features

- 🚀 **Easy Integration** - Install NuGet package and configure in 3 lines
- 📦 **MongoDB Optimized** - Native MongoDB support with proper indexing
- 📸 **Smart Snapshots** - Configurable snapshots for performance optimization
- 🏷️ **Event Kinds** - Auto-generated event categorization for filtering
- 🔒 **Type Safe** - Strongly typed aggregates and events with C# records
- ⚡ **Concurrency Control** - Built-in optimistic locking with versioning
- 🔍 **Query API** - Rich event querying for projections and read models
- 🔄 **Event Versioning** - Automatic upcasting for event schema evolution
- 🧩 **Extensible** - Provider pattern ready for SQL Server, PostgreSQL, etc.
- ✅ **Production Ready** - 94+ tests with continuous integration

## Installation

```bash
dotnet add package EventSourcing.MongoDB
```

## Quick Start

### 1. Configure Services

```csharp
using EventSourcing.MongoDB;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEventSourcing(config =>
{
    config.UseMongoDB("mongodb://localhost:27017", "eventstore")
          .RegisterEventsFromAssembly(typeof(Program).Assembly)
          .InitializeMongoDB("UserAggregate", "OrderAggregate");

    config.SnapshotEvery(10); // Snapshot every 10 events
});

var app = builder.Build();
app.Run();
```

### 2. Define Events

Events are immutable records that represent state changes:

```csharp
using EventSourcing.Core;

// User events
public record UserCreatedEvent(Guid UserId, string Email, string Name) : DomainEvent;
public record UserEmailChangedEvent(string NewEmail) : DomainEvent;

// Order events
public record OrderPlacedEvent(Guid OrderId, Guid CustomerId, decimal Total) : DomainEvent;
public record OrderShippedEvent(string TrackingNumber) : DomainEvent;
```

**Event Kinds** are auto-generated: `user.created`, `user.emailchanged`, `order.placed`, etc.

### 3. Create Aggregates

Aggregates maintain state and enforce business rules:

```csharp
using EventSourcing.Core;

public class UserAggregate : AggregateBase<Guid>
{
    public override Guid Id { get; protected set; }
    public string Email { get; protected set; } = string.Empty;
    public string Name { get; protected set; } = string.Empty;

    public void CreateUser(Guid userId, string email, string name)
    {
        if (Id != Guid.Empty)
            throw new InvalidOperationException("User already exists");

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));

        RaiseEvent(new UserCreatedEvent(userId, email, name));
    }

    public void ChangeEmail(string newEmail)
    {
        if (Email == newEmail) return; // No change

        RaiseEvent(new UserEmailChangedEvent(newEmail));
    }

    // Event handlers
    private void Apply(UserCreatedEvent e)
    {
        Id = e.UserId;
        Email = e.Email;
        Name = e.Name;
    }

    private void Apply(UserEmailChangedEvent e)
    {
        Email = e.NewEmail;
    }
}
```

### 4. Use in Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IAggregateRepository<UserAggregate, Guid> _repository;

    public UsersController(IAggregateRepository<UserAggregate, Guid> repository)
    {
        _repository = repository;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var userId = Guid.NewGuid();
        var user = new UserAggregate();
        user.CreateUser(userId, request.Email, request.Name);

        await _repository.SaveAsync(user);

        return CreatedAtAction(nameof(GetUser), new { id = userId }, user);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var user = await _repository.GetByIdAsync(id);
        return Ok(user);
    }

    [HttpPut("{id}/email")]
    public async Task<IActionResult> UpdateEmail(Guid id, [FromBody] UpdateEmailRequest request)
    {
        var user = await _repository.GetByIdAsync(id);
        user.ChangeEmail(request.Email);
        await _repository.SaveAsync(user);

        return Ok(user);
    }
}
```

## Example Application

The `EventSourcing.Example.Api` demonstrates a complete implementation with:

### **Two Aggregates**
- **UserAggregate** - User management with email, name, activation
- **OrderAggregate** - Order processing with items, shipping, payment

### **Features**
- ✅ REST API endpoints for both aggregates
- ✅ Event history queries per aggregate
- ✅ Global event queries with filtering
- ✅ Event categorization by kind
- ✅ Swagger/OpenAPI documentation
- ✅ Comprehensive .http test files

### **Run the Example**

```bash
# Start MongoDB (Docker)
docker run -d -p 27017:27017 mongo:7.0

# Run the API
cd examples/EventSourcing.Example.Api
dotnet run
```

Visit `http://localhost:5147/swagger` to explore the API.

### **Available Endpoints**

**Users:**
- `GET /api/users` - List all users
- `GET /api/users/{id}` - Get user by ID
- `POST /api/users` - Create user
- `PUT /api/users/{id}/email` - Update email
- `POST /api/users/{id}/activate` - Activate user
- `GET /api/users/{id}/events` - Get user event history

**Orders:**
- `GET /api/orders` - List all orders
- `GET /api/orders/{id}` - Get order by ID
- `POST /api/orders` - Create order
- `POST /api/orders/{id}/items` - Add items
- `POST /api/orders/{id}/ship` - Ship order
- `GET /api/orders/{id}/events` - Get order event history

**Events (Users):**
- `GET /api/events/users` - All user events
- `GET /api/events/users/{userId}` - Events for specific user
- `GET /api/events/users/kind/{kind}` - Filter by event kind (e.g., "user.created")
- `GET /api/events/users/kinds?kinds={kinds}` - Filter by multiple kinds (comma-separated)
- `GET /api/events/users/since?since={timestamp}` - Events since timestamp

**Events (Orders):**
- `GET /api/events/orders` - All order events
- `GET /api/events/orders/{orderId}` - Events for specific order
- `GET /api/events/orders/kind/{kind}` - Filter by event kind (e.g., "order.placed")
- `GET /api/events/orders/kinds?kinds={kinds}` - Filter by multiple kinds (comma-separated)
- `GET /api/events/orders/since?since={timestamp}` - Events since timestamp

## Core Concepts

### Event Sourcing Pattern

Instead of storing just the current state, event sourcing stores **all state changes** as immutable events.

```
Traditional Storage:
┌──────────────────────┐
│   User Table         │
├──────────────────────┤
│ Id: 123              │
│ Email: new@email.com │
│ Name: John Doe       │
└──────────────────────┘

Event Sourcing:
┌─────────────────────────────────────┐
│ UserCreated(123, "john@email.com")  │
│ EmailChanged("new@email.com")       │
│ NameChanged("John Doe")             │
└─────────────────────────────────────┘
```

**Benefits:**
- Complete audit trail of all changes
- Reconstruct state at any point in time
- Event replay for debugging and testing
- Build multiple read models from same events

### Aggregates

An aggregate is a cluster of domain objects treated as a single unit:

- **Identity** - Unique identifier (Guid, int, string, custom type)
- **Version** - Monotonic version number for optimistic concurrency
- **Events** - Uncommitted domain events representing pending changes
- **State** - Reconstructed by replaying all events

### Snapshots

Snapshots optimize performance by storing point-in-time state:

```csharp
config.SnapshotEvery(10); // Create snapshot every 10 events
```

**How it works:**
1. When loading: Retrieve latest snapshot + subsequent events
2. When saving: If `version % 10 == 0`, create snapshot
3. State reconstruction: Apply snapshot → replay events since snapshot

**Performance impact:**
- Without snapshots: Replay 1000 events (slow)
- With `SnapshotEvery(10)`: Replay max 10 events (fast)
- With `SnapshotEvery(1)`: Replay 1 event (fastest reads, many writes)

**Recommended:** `SnapshotEvery(10)` to `SnapshotEvery(50)` depending on complexity.

### Optimistic Concurrency

Version-based concurrency control prevents conflicts:

```csharp
// Thread 1
var user = await repo.GetByIdAsync(id); // Version = 5
user.ChangeEmail("new@email.com");
await repo.SaveAsync(user); // Version = 6 ✓

// Thread 2 (concurrent modification)
var user = await repo.GetByIdAsync(id); // Version = 5
user.ChangeName("Alice");
await repo.SaveAsync(user); // ❌ ConcurrencyException!
```

Handle with retry logic or conflict resolution strategies.

## Event Queries for CQRS

Build optimized read models using event queries:

```csharp
// Get all events for projection building
var allEvents = await eventStore.GetAllEventsAsync("UserAggregate");

// Incremental processing
var recentEvents = await eventStore.GetAllEventsAsync(
    "UserAggregate",
    DateTimeOffset.UtcNow.AddHours(-1)
);

// Filter by event kind
var createdEvents = await eventStore.GetEventsByKindAsync(
    "UserAggregate",
    "user.created"
);

// Multiple kinds
var events = await eventStore.GetEventsByKindsAsync(
    "UserAggregate",
    new[] { "user.created", "user.emailchanged" }
);
```

### Building Projections

```csharp
public class UserListProjection
{
    private readonly IEventStore _eventStore;
    private readonly IMongoCollection<UserListItem> _collection;

    public async Task RebuildAsync()
    {
        var events = await _eventStore.GetAllEventsAsync("UserAggregate");

        foreach (var evt in events)
        {
            switch (evt)
            {
                case UserCreatedEvent e:
                    await _collection.InsertOneAsync(new UserListItem
                    {
                        Id = e.UserId,
                        Email = e.Email,
                        Name = e.Name
                    });
                    break;

                case UserEmailChangedEvent e:
                    await _collection.UpdateOneAsync(
                        u => u.Id == e.UserId,
                        Builders<UserListItem>.Update.Set(u => u.Email, e.NewEmail)
                    );
                    break;
            }
        }
    }
}
```

## MongoDB Collections & Indexes

For each aggregate type, two collections are created:

```
{aggregateType}_events      - Append-only event log
{aggregateType}_snapshots   - Point-in-time state captures
```

**Example:**
```
useraggregate_events
useraggregate_snapshots
orderaggregate_events
orderaggregate_snapshots
```

**Automatically created indexes:**

Events:
- `{ aggregateId: 1, version: 1 }` (unique) - Fast aggregate loading
- `{ timestamp: 1 }` - Time-based queries
- `{ kind: 1 }` - Event kind filtering

Snapshots:
- `{ aggregateId: 1, aggregateType: 1 }` (unique) - Fast snapshot retrieval

## Testing

The library includes 85+ tests with comprehensive coverage:

```bash
dotnet test
```

**Example tests:**

```csharp
[Fact]
public void UserAggregate_CreateUser_ShouldRaiseEvent()
{
    // Arrange
    var aggregate = new UserAggregate();
    var userId = Guid.NewGuid();

    // Act
    aggregate.CreateUser(userId, "test@example.com", "Test User");

    // Assert
    aggregate.Id.Should().Be(userId);
    aggregate.Email.Should().Be("test@example.com");
    aggregate.GetUncommittedEvents().Should().HaveCount(1);
}

[Fact]
public async Task Repository_ConcurrencyConflict_ShouldThrow()
{
    // Arrange
    var user = new UserAggregate();
    user.CreateUser(Guid.NewGuid(), "test@example.com", "Test");
    await _repository.SaveAsync(user);

    // Act - Concurrent modifications
    var user1 = await _repository.GetByIdAsync(user.Id);
    var user2 = await _repository.GetByIdAsync(user.Id);

    user1.ChangeEmail("new1@example.com");
    await _repository.SaveAsync(user1);

    user2.ChangeEmail("new2@example.com");

    // Assert
    await Assert.ThrowsAsync<ConcurrencyException>(() =>
        _repository.SaveAsync(user2)
    );
}
```

## Best Practices

### ✅ DO: Name events in past tense
```csharp
public record UserCreatedEvent(...) : DomainEvent;
public record OrderPlacedEvent(...) : DomainEvent;
```

### ❌ DON'T: Name events in imperative
```csharp
public record CreateUserEvent(...) : DomainEvent; // Wrong!
```

### ✅ DO: Keep events small and focused
```csharp
public record UserEmailChangedEvent(string NewEmail) : DomainEvent;
public record UserNameChangedEvent(string NewName) : DomainEvent;
```

### ❌ DON'T: Create kitchen-sink events
```csharp
public record UserUpdatedEvent(string? Name, string? Email, bool? Active) : DomainEvent; // Wrong!
```

### ✅ DO: Use snapshots wisely
```csharp
config.SnapshotEvery(10); // Good for most cases
```

### ❌ DON'T: Snapshot on every event
```csharp
config.SnapshotEvery(1); // Excessive write overhead!
```

### ✅ DO: Handle concurrency with retries
```csharp
for (int i = 0; i < 3; i++)
{
    try
    {
        var user = await _repo.GetByIdAsync(id);
        user.ChangeEmail(newEmail);
        await _repo.SaveAsync(user);
        return;
    }
    catch (ConcurrencyException) when (i < 2)
    {
        await Task.Delay(100 * (i + 1));
    }
}
```

### ✅ DO: Build projections for queries
```csharp
// Good - Query optimized read model
var users = await _userReadModel.GetActiveUsersAsync();
```

### ❌ DON'T: Load aggregates for queries
```csharp
// Bad - Loading all aggregates
var allUsers = await _repo.GetAllAsync(); // No such method!
var activeUsers = allUsers.Where(u => u.IsActive); // Wrong!
```

## Troubleshooting

### "Event type not registered"

**Error:**
```
System.InvalidOperationException: Event type 'UserCreatedEvent' is not registered.
```

**Solution:**
```csharp
builder.Services.AddEventSourcing(config =>
{
    config.UseMongoDB(...)
          .RegisterEventsFromAssembly(typeof(Program).Assembly);
});
```

### "Concurrency conflicts are frequent"

**Solutions:**
1. Implement retry logic
2. Split large aggregates into smaller ones
3. Use eventual consistency between aggregates
4. Consider using sagas for long-running processes

### "Slow event replay"

**Solutions:**
1. Increase snapshot frequency: `config.SnapshotEvery(5);`
2. Verify indexes: Check MongoDB indexes are created
3. Optimize event handlers: Remove heavy computations

## Performance

**Event Store Operations:**
- Append events: **O(1)** - Very fast, append-only
- Load aggregate: **O(log n)** - Fast with indexes + snapshots
- Query all events: **O(n)** - Full collection scan (use projections!)

**Optimization Tips:**
1. ✅ Use snapshots to reduce event replay
2. ✅ Build read models for queries (CQRS)
3. ✅ Call `InitializeMongoDB()` to ensure indexes
4. ✅ Use connection pooling (automatic with MongoDB driver)
5. ✅ Consider batch operations for bulk processing

## CI/CD Integration

This project uses GitHub Actions for:

- ✅ **Continuous Integration** - Build and test on every push/PR
- ✅ **Code Coverage** - Track test coverage with reports
- ✅ **Automated Releases** - NuGet packages published on tags

See `.github/workflows/` for workflow configurations.

## Roadmap

- [ ] SQL Server provider
- [ ] PostgreSQL provider
- [x] Event versioning and upcasting ✅
- [ ] Saga pattern support
- [ ] Built-in projection framework
- [ ] Event subscriptions/notifications
- [ ] Migration utilities

## Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Submit a pull request

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Documentation

### 📚 Guides

- **[Event Versioning & Upcasting](docs/EVENT_VERSIONING.md)** - Evolve event schemas over time with automatic transformations
- **[Creating Custom Providers](docs/CUSTOM_PROVIDERS.md)** - Build your own storage provider for any database
- **[Multi-Database Usage](docs/MULTI_DB_USAGE.md)** - Use different databases for different aggregates
- **[Release Process](.github/RELEASE.md)** - How to create and publish releases

### 📖 Core Documentation

- **This README** - Quick start and core concepts
- **Example Application** - `examples/EventSourcing.Example.Api/`
- **API Documentation** - Swagger UI at `/swagger` when running the example

## Support

- **Issues**: [GitHub Issues](https://github.com/Dyshay/EventSourcing/issues)
- **Discussions**: [GitHub Discussions](https://github.com/Dyshay/EventSourcing/discussions)
- **Example**: `examples/EventSourcing.Example.Api/`

---

**Built with ❤️ for the .NET community**
