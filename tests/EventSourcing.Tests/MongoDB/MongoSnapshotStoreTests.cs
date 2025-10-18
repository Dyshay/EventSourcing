using EventSourcing.Abstractions;
using EventSourcing.Core;
using EventSourcing.MongoDB;
using FluentAssertions;
using MongoDB.Driver;
using Xunit;

namespace EventSourcing.Tests.MongoDB;

[Collection("MongoDB Collection")]
public class MongoSnapshotStoreTests : IAsyncLifetime
{
    private readonly IMongoDatabase _database;
    private readonly MongoSnapshotStore _snapshotStore;
    private const string TestDatabaseName = "test";

    public class TestAggregate : IAggregate<Guid>
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Counter { get; set; }
        public int Version { get; set; }
        public IReadOnlyList<IEvent> UncommittedEvents => new List<IEvent>();

        public TestAggregate() { }

        public TestAggregate(Guid id, string name, int counter)
        {
            Id = id;
            Name = name;
            Counter = counter;
        }

        public void RaiseEvent(IEvent @event) { }
        public void MarkEventsAsCommitted() { }
        public void LoadFromHistory(IEnumerable<IEvent> events) { }
    }

    public MongoSnapshotStoreTests()
    {
        var connectionString = TestHelpers.MongoDbFixture.GetConnectionString();
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(TestDatabaseName);
        _snapshotStore = new MongoSnapshotStore(_database);
    }

    public async Task InitializeAsync()
    {
        await _snapshotStore.EnsureIndexesAsync("TestAggregate");
    }

    public async Task DisposeAsync()
    {
        // Don't drop the entire database when using shared "test" database
        // Just clean up the test collections
        try
        {
            await _database.DropCollectionAsync("testaggregate_snapshots");
            await _database.DropCollectionAsync("testaggregate1_snapshots");
            await _database.DropCollectionAsync("testaggregate2_snapshots");
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task SaveSnapshotAsync_ShouldStoreSnapshot()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var aggregate = new TestAggregate(aggregateId, "Test Name", 42);

        // Act
        await _snapshotStore.SaveSnapshotAsync(aggregateId, "TestAggregate", aggregate, version: 10);

        // Assert
        var snapshot = await _snapshotStore.GetLatestSnapshotAsync<Guid, TestAggregate>(aggregateId, "TestAggregate");
        snapshot.Should().NotBeNull();
        var snapshotData = snapshot!;
        snapshotData.Version.Should().Be(10);
        snapshotData.Aggregate.Should().NotBeNull();
        snapshotData.Aggregate.Id.Should().Be(aggregateId);
        snapshotData.Aggregate.Name.Should().Be("Test Name");
        snapshotData.Aggregate.Counter.Should().Be(42);
    }

    [Fact]
    public async Task SaveSnapshotAsync_MultipleTimes_ShouldOverwritePrevious()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var aggregate1 = new TestAggregate(aggregateId, "Version 1", 10);
        var aggregate2 = new TestAggregate(aggregateId, "Version 2", 20);

        // Act
        await _snapshotStore.SaveSnapshotAsync(aggregateId, "TestAggregate", aggregate1, version: 10);
        await _snapshotStore.SaveSnapshotAsync(aggregateId, "TestAggregate", aggregate2, version: 20);

        // Assert
        var snapshot = await _snapshotStore.GetLatestSnapshotAsync<Guid, TestAggregate>(aggregateId, "TestAggregate");
        snapshot.Should().NotBeNull();
        var snapshotData = snapshot!;
        snapshotData.Version.Should().Be(20);
        snapshotData.Aggregate.Name.Should().Be("Version 2");
        snapshotData.Aggregate.Counter.Should().Be(20);
    }

    [Fact]
    public async Task GetLatestSnapshotAsync_WithNonExistentAggregate_ShouldReturnNull()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();

        // Act
        var result = await _snapshotStore.GetLatestSnapshotAsync<Guid, TestAggregate>(aggregateId, "TestAggregate");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAndGetSnapshot_WithComplexAggregate_ShouldPreserveData()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var aggregate = new TestAggregate(aggregateId, "Complex Name ñ é à", 9999);

        // Act
        await _snapshotStore.SaveSnapshotAsync(aggregateId, "TestAggregate", aggregate, version: 100);
        var retrieved = await _snapshotStore.GetLatestSnapshotAsync<Guid, TestAggregate>(aggregateId, "TestAggregate");

        // Assert
        retrieved.Should().NotBeNull();
        var snapshotData = retrieved!;
        snapshotData.Aggregate.Id.Should().Be(aggregateId);
        snapshotData.Aggregate.Name.Should().Be("Complex Name ñ é à");
        snapshotData.Aggregate.Counter.Should().Be(9999);
    }

    [Fact]
    public async Task SaveSnapshot_WithDifferentAggregateTypes_ShouldIsolate()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var aggregate1 = new TestAggregate(aggregateId, "Type1", 1);
        var aggregate2 = new TestAggregate(aggregateId, "Type2", 2);

        // Act
        await _snapshotStore.SaveSnapshotAsync(aggregateId, "TestAggregate1", aggregate1, version: 10);
        await _snapshotStore.SaveSnapshotAsync(aggregateId, "TestAggregate2", aggregate2, version: 20);

        // Assert
        var snapshot1 = await _snapshotStore.GetLatestSnapshotAsync<Guid, TestAggregate>(aggregateId, "TestAggregate1");
        var snapshot2 = await _snapshotStore.GetLatestSnapshotAsync<Guid, TestAggregate>(aggregateId, "TestAggregate2");

        snapshot1!.Aggregate.Counter.Should().Be(1);
        snapshot2!.Aggregate.Counter.Should().Be(2);
    }

    [Fact]
    public async Task EnsureIndexesAsync_ShouldCreateCollection()
    {
        // Arrange & Act
        await _snapshotStore.EnsureIndexesAsync("TestAggregate");

        // Assert - Verify collection exists
        var collections = await _database.ListCollectionNames().ToListAsync();
        collections.Should().Contain("testaggregate_snapshots");
    }

    [Fact]
    public async Task SaveSnapshot_WithZeroVersion_ShouldWork()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var aggregate = new TestAggregate(aggregateId, "Zero", 0);

        // Act
        await _snapshotStore.SaveSnapshotAsync(aggregateId, "TestAggregate", aggregate, version: 0);

        // Assert
        var snapshot = await _snapshotStore.GetLatestSnapshotAsync<Guid, TestAggregate>(aggregateId, "TestAggregate");
        snapshot.Should().NotBeNull();
        snapshot!.Version.Should().Be(0);
    }

    [Fact]
    public async Task SaveSnapshot_WithLargeVersion_ShouldWork()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var aggregate = new TestAggregate(aggregateId, "Large", 999);

        // Act
        await _snapshotStore.SaveSnapshotAsync(aggregateId, "TestAggregate", aggregate, version: 999999);

        // Assert
        var snapshot = await _snapshotStore.GetLatestSnapshotAsync<Guid, TestAggregate>(aggregateId, "TestAggregate");
        snapshot.Should().NotBeNull();
        snapshot!.Version.Should().Be(999999);
    }
}
