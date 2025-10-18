using EventSourcing.Abstractions;
using EventSourcing.Core;
using EventSourcing.MongoDB;
using FluentAssertions;
using MongoDB.Driver;
using Xunit;

namespace EventSourcing.Tests.MongoDB;

[Collection("MongoDB Collection")]
public class MongoEventStoreTests : IAsyncLifetime
{
    private readonly IMongoDatabase _database;
    private readonly MongoEventStore _eventStore;
    private const string TestDatabaseName = "test";

    public record TestEventV1(string Data) : DomainEvent;
    public record TestEventV2(int Value) : DomainEvent;

    public MongoEventStoreTests()
    {
        var connectionString = TestHelpers.MongoDbFixture.GetConnectionString();
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(TestDatabaseName);
        _eventStore = new MongoEventStore(_database);

        // Register test event types
        EventSourcing.MongoDB.Serialization.EventSerializer.RegisterEventType(typeof(TestEventV1));
        EventSourcing.MongoDB.Serialization.EventSerializer.RegisterEventType(typeof(TestEventV2));
    }

    public async Task InitializeAsync()
    {
        // Drop the collection first to ensure a clean state
        try
        {
            await _database.DropCollectionAsync("testaggregate_events");
        }
        catch
        {
            // Ignore if collection doesn't exist
        }

        await _eventStore.EnsureIndexesAsync("TestAggregate");
    }

    public async Task DisposeAsync()
    {
        // Don't drop the entire database when using shared "test" database
        // Just clean up the test collections
        try
        {
            await _database.DropCollectionAsync("testaggregate_events");
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task AppendEventsAsync_ShouldStoreEvents()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var events = new List<IEvent>
        {
            new TestEventV1("Event 1"),
            new TestEventV1("Event 2")
        };

        // Act
        await _eventStore.AppendEventsAsync(aggregateId, "TestAggregate", events, expectedVersion: 0);

        // Assert
        var storedEvents = await _eventStore.GetEventsAsync(aggregateId, "TestAggregate");
        storedEvents.Should().HaveCount(2);
        storedEvents.Cast<TestEventV1>().First().Data.Should().Be("Event 1");
        storedEvents.Cast<TestEventV1>().Last().Data.Should().Be("Event 2");
    }

    [Fact]
    public async Task AppendEventsAsync_WithWrongVersion_ShouldThrowConcurrencyException()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var firstEvents = new List<IEvent> { new TestEventV1("First") };
        var secondEvents = new List<IEvent> { new TestEventV1("Second") };

        await _eventStore.AppendEventsAsync(aggregateId, "TestAggregate", firstEvents, expectedVersion: 0);

        // Act & Assert
        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            _eventStore.AppendEventsAsync(aggregateId, "TestAggregate", secondEvents, expectedVersion: 0)
        );
    }

    [Fact]
    public async Task AppendEventsAsync_WithEmptyEvents_ShouldDoNothing()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var events = new List<IEvent>();

        // Act
        await _eventStore.AppendEventsAsync(aggregateId, "TestAggregate", events, expectedVersion: 0);

        // Assert
        var storedEvents = await _eventStore.GetEventsAsync(aggregateId, "TestAggregate");
        storedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEventsAsync_WithFromVersion_ShouldReturnEventsAfterVersion()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var events = new List<IEvent>
        {
            new TestEventV1("Event 1"),
            new TestEventV1("Event 2"),
            new TestEventV1("Event 3")
        };
        await _eventStore.AppendEventsAsync(aggregateId, "TestAggregate", events, expectedVersion: 0);

        // Act
        var result = await _eventStore.GetEventsAsync(aggregateId, "TestAggregate", fromVersion: 1);

        // Assert
        result.Should().HaveCount(2);
        result.Cast<TestEventV1>().First().Data.Should().Be("Event 2");
        result.Cast<TestEventV1>().Last().Data.Should().Be("Event 3");
    }

    [Fact]
    public async Task GetEventsAsync_WithNonExistentAggregate_ShouldReturnEmpty()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();

        // Act
        var result = await _eventStore.GetEventsAsync(aggregateId, "TestAggregate");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllEventsAsync_ShouldReturnAllEvents()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await _eventStore.AppendEventsAsync(id1, "TestAggregate", new[] { new TestEventV1("A1") }, 0);
        await _eventStore.AppendEventsAsync(id2, "TestAggregate", new[] { new TestEventV1("A2") }, 0);

        // Act
        var result = await _eventStore.GetAllEventsAsync("TestAggregate");

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllEventsAsync_WithTimestamp_ShouldReturnEventsSinceTimestamp()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var beforeTimestamp = DateTimeOffset.UtcNow;

        await Task.Delay(100); // Ensure timestamp difference

        await _eventStore.AppendEventsAsync(
            aggregateId,
            "TestAggregate",
            new[] { new TestEventV1("After") },
            0
        );

        // Act
        var result = await _eventStore.GetAllEventsAsync("TestAggregate", beforeTimestamp);

        // Assert
        result.Should().HaveCount(1);
        result.Cast<TestEventV1>().First().Data.Should().Be("After");
    }

    [Fact]
    public async Task GetEventsByKindAsync_ShouldFilterByKind()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        await _eventStore.AppendEventsAsync(
            aggregateId,
            "TestAggregate",
            new IEvent[] { new TestEventV1("Data"), new TestEventV2(42) },
            0
        );

        // Act
        var result = await _eventStore.GetEventsByKindAsync("TestAggregate", "test.eventv1");

        // Assert
        result.Should().HaveCount(1);
        result.First().Should().BeOfType<TestEventV1>();
    }

    [Fact]
    public async Task GetEventsByKindsAsync_ShouldFilterByMultipleKinds()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        await _eventStore.AppendEventsAsync(
            aggregateId,
            "TestAggregate",
            new IEvent[]
            {
                new TestEventV1("Data1"),
                new TestEventV2(42),
                new TestEventV1("Data2")
            },
            0
        );

        // Act
        var result = await _eventStore.GetEventsByKindsAsync(
            "TestAggregate",
            new[] { "test.eventv1", "test.eventv2" }
        );

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAggregateIdsAsync_ShouldReturnDistinctIds()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await _eventStore.AppendEventsAsync(id1, "TestAggregate", new[] { new TestEventV1("A") }, 0);
        await _eventStore.AppendEventsAsync(id1, "TestAggregate", new[] { new TestEventV1("B") }, 1);
        await _eventStore.AppendEventsAsync(id2, "TestAggregate", new[] { new TestEventV1("C") }, 0);

        // Act
        var result = await _eventStore.GetAllAggregateIdsAsync("TestAggregate");

        // Assert
        var ids = result.ToList();
        ids.Should().HaveCount(2);
        ids.Should().Contain(id1.ToString());
        ids.Should().Contain(id2.ToString());
    }

    [Fact]
    public async Task GetEventEnvelopesAsync_ShouldReturnEnvelopesWithMetadata()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        await _eventStore.AppendEventsAsync(
            aggregateId,
            "TestAggregate",
            new[] { new TestEventV1("Test Data") },
            0
        );

        // Act
        var result = await _eventStore.GetEventEnvelopesAsync(aggregateId, "TestAggregate");

        // Assert
        result.Should().HaveCount(1);
        var envelope = result.First();
        envelope.EventType.Should().Be("TestEventV1");
        envelope.Kind.Should().Be("test.eventv1");
        envelope.EventId.Should().NotBeEmpty();
        envelope.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetAllEventEnvelopesAsync_ShouldReturnAllEnvelopes()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await _eventStore.AppendEventsAsync(id1, "TestAggregate", new[] { new TestEventV1("A") }, 0);
        await _eventStore.AppendEventsAsync(id2, "TestAggregate", new[] { new TestEventV2(42) }, 0);

        // Act
        var result = await _eventStore.GetAllEventEnvelopesAsync("TestAggregate");

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllEventEnvelopesAsync_WithTimestamp_ShouldFilterByTimestamp()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var beforeTimestamp = DateTimeOffset.UtcNow;

        await Task.Delay(100);

        await _eventStore.AppendEventsAsync(
            aggregateId,
            "TestAggregate",
            new[] { new TestEventV1("After") },
            0
        );

        // Act
        var result = await _eventStore.GetAllEventEnvelopesAsync("TestAggregate", beforeTimestamp);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetEventEnvelopesByKindAsync_ShouldFilterEnvelopesByKind()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        await _eventStore.AppendEventsAsync(
            aggregateId,
            "TestAggregate",
            new IEvent[] { new TestEventV1("Data"), new TestEventV2(42) },
            0
        );

        // Act
        var result = await _eventStore.GetEventEnvelopesByKindAsync("TestAggregate", "test.eventv1");

        // Assert
        result.Should().HaveCount(1);
        result.First().EventType.Should().Be("TestEventV1");
    }

    [Fact]
    public async Task GetEventEnvelopesByKindsAsync_ShouldFilterByMultipleKinds()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        await _eventStore.AppendEventsAsync(
            aggregateId,
            "TestAggregate",
            new IEvent[]
            {
                new TestEventV1("Data1"),
                new TestEventV2(42),
                new TestEventV1("Data2")
            },
            0
        );

        // Act
        var result = await _eventStore.GetEventEnvelopesByKindsAsync(
            "TestAggregate",
            new[] { "test.eventv1" }
        );

        // Assert
        result.Should().HaveCount(2);
        result.All(e => e.EventType == "TestEventV1").Should().BeTrue();
    }

    [Fact]
    public async Task EnsureIndexesAsync_ShouldCreateIndexes()
    {
        // Arrange & Act
        await _eventStore.EnsureIndexesAsync("TestAggregate");

        // Assert - Verify collection exists
        var collections = await _database.ListCollectionNames().ToListAsync();
        collections.Should().Contain("testaggregate_events");
    }

    [Fact]
    public async Task AppendEventsAsync_WithMultipleAggregates_ShouldIsolateEvents()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await _eventStore.AppendEventsAsync(id1, "TestAggregate", new[] { new TestEventV1("A1") }, 0);
        await _eventStore.AppendEventsAsync(id2, "TestAggregate", new[] { new TestEventV1("A2") }, 0);

        // Act
        var events1 = await _eventStore.GetEventsAsync(id1, "TestAggregate");
        var events2 = await _eventStore.GetEventsAsync(id2, "TestAggregate");

        // Assert
        events1.Should().HaveCount(1);
        events1.Cast<TestEventV1>().First().Data.Should().Be("A1");

        events2.Should().HaveCount(1);
        events2.Cast<TestEventV1>().First().Data.Should().Be("A2");
    }
}
