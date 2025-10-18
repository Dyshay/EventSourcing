# Event Versioning and Upcasting

Event Sourcing systems store events as an immutable audit log. Over time, your business requirements change, and you may need to modify event schemas. Event versioning and upcasting allow you to evolve event schemas while maintaining compatibility with historical events.

## Table of Contents

- [The Problem](#the-problem)
- [The Solution](#the-solution)
- [How It Works](#how-it-works)
- [Usage Guide](#usage-guide)
- [Best Practices](#best-practices)
- [Examples](#examples)

## The Problem

In traditional databases, you can modify schemas with migrations. With Event Sourcing, events are immutable. When you need to change an event's structure, you face challenges:

```csharp
// Original event (stored in database)
public record UserCreatedEvent(Guid UserId, string Name);

// New requirement: split name into first and last name
public record UserCreatedEvent(Guid UserId, string FirstName, string LastName);
```

**Problem**: Historical events in your event store still use the old format with a single `Name` field.

## The Solution

**Event Upcasting** automatically transforms old event versions to new versions when reading from the event store.

```
[Old Event V1] → [Upcaster V1→V2] → [New Event V2]
```

Benefits:
- ✅ Historical events remain immutable
- ✅ New code works with latest event versions
- ✅ Automatic transformation on read
- ✅ Support for multi-step version chains (V1 → V2 → V3)

## How It Works

### Architecture

1. **Event Versions**: Create separate classes for each version
2. **Upcasters**: Define transformations between versions
3. **Registry**: Central registry manages all upcasters
4. **Automatic Application**: MongoEventStore applies upcasting when reading events

```
┌─────────────┐
│  Database   │
│ (Old Events)│
└──────┬──────┘
       │
       ▼
┌─────────────────┐
│  MongoEventStore│
└──────┬──────────┘
       │
       ▼
┌─────────────────────┐
│ EventUpcasterRegistry│ ◄── Registered Upcasters
└──────┬──────────────┘
       │
       ▼
┌──────────────┐
│ Latest Event │
│   Version    │
└──────────────┘
```

## Usage Guide

### Step 1: Define Event Versions

Create separate record types for each version:

```csharp
// V1 - Original version
public record UserCreatedEventV1(Guid UserId, string Name) : DomainEvent;

// V2 - Split name
public record UserCreatedEventV2(Guid UserId, string FirstName, string LastName) : DomainEvent;

// V3 - Add email
public record UserCreatedEventV3(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email
) : DomainEvent;
```

### Step 2: Create Upcasters

Implement `EventUpcaster<TSource, TTarget>` for each transformation:

```csharp
using EventSourcing.Core.Versioning;

public class UserCreatedV1ToV2Upcaster : EventUpcaster<UserCreatedEventV1, UserCreatedEventV2>
{
    public override UserCreatedEventV2 Upcast(UserCreatedEventV1 oldEvent)
    {
        var nameParts = oldEvent.Name.Split(' ', 2);
        var firstName = nameParts.Length > 0 ? nameParts[0] : string.Empty;
        var lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

        return new UserCreatedEventV2(oldEvent.UserId, firstName, lastName);
    }
}

public class UserCreatedV2ToV3Upcaster : EventUpcaster<UserCreatedEventV2, UserCreatedEventV3>
{
    public override UserCreatedEventV3 Upcast(UserCreatedEventV2 oldEvent)
    {
        var email = $"{oldEvent.FirstName.ToLower()}.{oldEvent.LastName.ToLower()}@example.com";
        return new UserCreatedEventV3(
            oldEvent.UserId,
            oldEvent.FirstName,
            oldEvent.LastName,
            email
        );
    }
}
```

### Step 3: Register Upcasters

Configure upcasters during application startup:

```csharp
services.AddEventSourcing(config =>
{
    config.UseMongoDB(connectionString, databaseName)
          .EnableEventVersioning()  // ← Enable versioning
          .AddUpcaster(new UserCreatedV1ToV2Upcaster())  // ← Register upcasters
          .AddUpcaster(new UserCreatedV2ToV3Upcaster())
          .SnapshotEvery(10);
});
```

### Step 4: Update Your Aggregate

Your aggregate only needs to handle the latest version:

```csharp
public class UserAggregate : AggregateBase<Guid>
{
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;

    // Only implement Apply for the latest version!
    private void Apply(UserCreatedEventV3 @event)
    {
        Id = @event.UserId;
        FirstName = @event.FirstName;
        LastName = @event.LastName;
        Email = @event.Email;
    }

    // No need to handle V1 or V2 - they're automatically upcasted to V3
}
```

### Step 5: Automatic Upcasting

When loading aggregates, old events are automatically transformed:

```csharp
// User stored with V1 event
var user = await repository.GetByIdAsync(userId);

// Behind the scenes:
// 1. MongoEventStore reads UserCreatedEventV1 from database
// 2. V1→V2 upcaster transforms to V2
// 3. V2→V3 upcaster transforms to V3
// 4. Aggregate receives UserCreatedEventV3
```

## Best Practices

### 1. Never Delete Old Event Classes

Keep all event versions for historical reference:

```csharp
// ✅ GOOD - Keep all versions
public record UserCreatedEventV1(...);
public record UserCreatedEventV2(...);
public record UserCreatedEventV3(...);

// ❌ BAD - Don't delete old versions
// Deleted UserCreatedEventV1 - events in DB won't deserialize!
```

### 2. Use Clear Naming Conventions

Include version numbers in class names:

```csharp
// ✅ GOOD - Clear versioning
public record OrderPlacedEventV1(...);
public record OrderPlacedEventV2(...);

// ❌ BAD - Unclear versioning
public record OrderPlacedEvent(...);
public record OrderPlacedEventNew(...);
public record OrderPlacedEvent2(...);
```

### 3. Chain Upcasters

Build transformation chains rather than direct V1→V3:

```csharp
// ✅ GOOD - Chain transformations
V1→V2 upcaster
V2→V3 upcaster
V3→V4 upcaster
// Any version automatically reaches V4

// ❌ BAD - Maintain every combination
V1→V4 upcaster
V2→V4 upcaster
V3→V4 upcaster
```

### 4. Test Upcasters Thoroughly

Ensure transformations preserve business meaning:

```csharp
[Fact]
public void Upcaster_ShouldPreserveBusinessData()
{
    // Arrange
    var v1 = new UserCreatedEventV1(userId, "John Doe");
    var upcaster = new UserCreatedV1ToV2Upcaster();

    // Act
    var v2 = upcaster.Upcast(v1);

    // Assert
    v2.UserId.Should().Be(v1.UserId);  // ← Preserve identity
    v2.FirstName.Should().Be("John");
    v2.LastName.Should().Be("Doe");
}
```

### 5. Handle Default Values Carefully

When adding new fields, choose sensible defaults:

```csharp
public class UserCreatedV2ToV3Upcaster : EventUpcaster<UserCreatedEventV2, UserCreatedEventV3>
{
    public override UserCreatedEventV3 Upcast(UserCreatedEventV2 oldEvent)
    {
        // ✅ GOOD - Meaningful default
        var email = $"{oldEvent.FirstName.ToLower()}.{oldEvent.LastName.ToLower()}@example.com";

        // ❌ BAD - Meaningless default
        // var email = "unknown@unknown.com";

        return new UserCreatedEventV3(
            oldEvent.UserId,
            oldEvent.FirstName,
            oldEvent.LastName,
            email
        );
    }
}
```

### 6. Document Breaking Changes

Add XML comments explaining transformations:

```csharp
/// <summary>
/// Upcasts UserCreatedEventV1 to V2 by splitting the single Name field
/// into FirstName and LastName using space as delimiter.
/// Names without spaces are treated as first name only.
/// </summary>
public class UserCreatedV1ToV2Upcaster : EventUpcaster<UserCreatedEventV1, UserCreatedEventV2>
{
    // ...
}
```

## Examples

### Example 1: Adding a Field

**Scenario**: Add `Email` field to user events

```csharp
// Old version (in database)
public record UserRegisteredV1(Guid UserId, string Username) : DomainEvent;

// New version (current code)
public record UserRegisteredV2(Guid UserId, string Username, string Email) : DomainEvent;

// Upcaster
public class UserRegisteredV1ToV2Upcaster : EventUpcaster<UserRegisteredV1, UserRegisteredV2>
{
    public override UserRegisteredV2 Upcast(UserRegisteredV1 old)
    {
        return new UserRegisteredV2(
            old.UserId,
            old.Username,
            Email: $"{old.Username}@legacy.com"  // Default email for old records
        );
    }
}
```

### Example 2: Renaming a Field

**Scenario**: Rename `Price` to `Amount`

```csharp
// Old version
public record ProductPricedV1(Guid ProductId, decimal Price) : DomainEvent;

// New version
public record ProductPricedV2(Guid ProductId, decimal Amount) : DomainEvent;

// Upcaster
public class ProductPricedV1ToV2Upcaster : EventUpcaster<ProductPricedV1, ProductPricedV2>
{
    public override ProductPricedV2 Upcast(ProductPricedV1 old)
    {
        return new ProductPricedV2(old.ProductId, Amount: old.Price);
    }
}
```

### Example 3: Splitting a Field

**Scenario**: Split `Address` into structured components

```csharp
// Old version
public record AddressChangedV1(string Address) : DomainEvent;

// New version
public record AddressChangedV2(
    string Street,
    string City,
    string PostalCode,
    string Country
) : DomainEvent;

// Upcaster with parsing logic
public class AddressChangedV1ToV2Upcaster : EventUpcaster<AddressChangedV1, AddressChangedV2>
{
    public override AddressChangedV2 Upcast(AddressChangedV1 old)
    {
        // Simple parsing (real implementation would be more robust)
        var parts = old.Address.Split(',').Select(p => p.Trim()).ToArray();

        return new AddressChangedV2(
            Street: parts.Length > 0 ? parts[0] : "",
            City: parts.Length > 1 ? parts[1] : "",
            PostalCode: parts.Length > 2 ? parts[2] : "",
            Country: parts.Length > 3 ? parts[3] : ""
        );
    }
}
```

### Example 4: Type Changes

**Scenario**: Change `Price` from `int` (cents) to `decimal` (dollars)

```csharp
// Old version (price in cents)
public record OrderTotalCalculatedV1(Guid OrderId, int TotalCents) : DomainEvent;

// New version (price in dollars)
public record OrderTotalCalculatedV2(Guid OrderId, decimal TotalDollars) : DomainEvent;

// Upcaster
public class OrderTotalV1ToV2Upcaster : EventUpcaster<OrderTotalCalculatedV1, OrderTotalCalculatedV2>
{
    public override OrderTotalCalculatedV2 Upcast(OrderTotalCalculatedV1 old)
    {
        return new OrderTotalCalculatedV2(
            old.OrderId,
            TotalDollars: old.TotalCents / 100m
        );
    }
}
```

## Advanced: Multi-Step Upcasting

The registry automatically chains upcasters:

```csharp
// Your database contains a mix of V1, V2, and V3 events
services.AddEventSourcing(config =>
{
    config.EnableEventVersioning()
          .AddUpcaster(new EventV1ToV2Upcaster())
          .AddUpcaster(new EventV2ToV3Upcaster())
          .AddUpcaster(new EventV3ToV4Upcaster());
});

// When loading:
// - V1 events: V1 → V2 → V3 → V4 (3 transformations)
// - V2 events: V2 → V3 → V4 (2 transformations)
// - V3 events: V3 → V4 (1 transformation)
// - V4 events: No transformation (already latest)
```

## Performance Considerations

- **Upcasting happens on read**, not on write
- **No database migration needed** - events stay in original format
- **Caching**: Consider snapshot strategy to avoid repeated upcasting
- **Max iterations**: Registry limits transformation chains to 100 steps to prevent infinite loops

## Troubleshooting

### Event Won't Deserialize

**Problem**: Old event class was deleted
**Solution**: Restore the old event class definition

### Upcaster Not Applied

**Problem**: Forgot to call `EnableEventVersioning()`
**Solution**: Add `.EnableEventVersioning()` to your configuration

### Circular Upcasting Error

**Problem**: Upcaster chain creates a loop (V1→V2→V1)
**Solution**: Review upcaster source/target types; ensure linear progression

## Summary

Event Versioning with Upcasting provides:
- ✅ Schema evolution without database migrations
- ✅ Backward compatibility with historical events
- ✅ Clean aggregate code (only latest version)
- ✅ Automatic transformation chains
- ✅ Type-safe transformations

Start using it today to future-proof your event-sourced system!
