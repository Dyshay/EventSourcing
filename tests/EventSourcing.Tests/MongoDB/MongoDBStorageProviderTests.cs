using EventSourcing.Abstractions;
using EventSourcing.MongoDB;
using FluentAssertions;
using MongoDB.Driver;
using Moq;

namespace EventSourcing.Tests.MongoDB;

public class MongoDBStorageProviderTests
{
    [Fact]
    public void Constructor_WithConnectionString_ShouldCreateProvider()
    {
        // Arrange & Act
        var act = () => new MongoDBStorageProvider("mongodb://localhost:27017", "TestDB");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithEmptyConnectionString_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var act = () => new MongoDBStorageProvider("", "TestDB");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Connection string*");
    }

    [Fact]
    public void Constructor_WithEmptyDatabaseName_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var act = () => new MongoDBStorageProvider("mongodb://localhost:27017", "");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Database name*");
    }

    [Fact]
    public void Constructor_WithNullDatabase_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        var act = () => new MongoDBStorageProvider((IMongoDatabase)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateEventStore_ShouldReturnEventStore()
    {
        // Arrange
        var mockDatabase = new Mock<IMongoDatabase>();
        var provider = new MongoDBStorageProvider(mockDatabase.Object);

        // Act
        var eventStore = provider.CreateEventStore();

        // Assert
        eventStore.Should().NotBeNull();
        eventStore.Should().BeAssignableTo<IEventStore>();
    }

    [Fact]
    public void CreateSnapshotStore_ShouldReturnSnapshotStore()
    {
        // Arrange
        var mockDatabase = new Mock<IMongoDatabase>();
        var provider = new MongoDBStorageProvider(mockDatabase.Object);

        // Act
        var snapshotStore = provider.CreateSnapshotStore();

        // Assert
        snapshotStore.Should().NotBeNull();
        snapshotStore.Should().BeAssignableTo<ISnapshotStore>();
    }

    [Fact]
    public void CreateEventStore_CalledMultipleTimes_ShouldReturnSameInstance()
    {
        // Arrange
        var mockDatabase = new Mock<IMongoDatabase>();
        var provider = new MongoDBStorageProvider(mockDatabase.Object);

        // Act
        var eventStore1 = provider.CreateEventStore();
        var eventStore2 = provider.CreateEventStore();

        // Assert
        eventStore1.Should().BeSameAs(eventStore2);
    }

    [Fact]
    public void CreateSnapshotStore_CalledMultipleTimes_ShouldReturnSameInstance()
    {
        // Arrange
        var mockDatabase = new Mock<IMongoDatabase>();
        var provider = new MongoDBStorageProvider(mockDatabase.Object);

        // Act
        var snapshotStore1 = provider.CreateSnapshotStore();
        var snapshotStore2 = provider.CreateSnapshotStore();

        // Assert
        snapshotStore1.Should().BeSameAs(snapshotStore2);
    }

    [Fact]
    public void ValidateConfiguration_WithValidDatabase_ShouldNotThrow()
    {
        // Arrange
        var mockDatabase = new Mock<IMongoDatabase>();
        var provider = new MongoDBStorageProvider(mockDatabase.Object);

        // Act
        var act = () => provider.ValidateConfiguration();

        // Assert
        act.Should().NotThrow();
    }
}
