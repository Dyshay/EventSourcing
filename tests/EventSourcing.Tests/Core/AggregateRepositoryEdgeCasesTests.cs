using EventSourcing.Abstractions;
using EventSourcing.Core;
using EventSourcing.Core.Publishing;
using EventSourcing.Core.Snapshots;
using EventSourcing.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace EventSourcing.Tests.Core;

public class AggregateRepositoryEdgeCasesTests
{
    private readonly Mock<IEventStore> _mockEventStore;
    private readonly Mock<ISnapshotStore> _mockSnapshotStore;
    private readonly Mock<ISnapshotStrategy> _mockSnapshotStrategy;
    private readonly AggregateRepository<TestAggregate, Guid> _repository;

    public AggregateRepositoryEdgeCasesTests()
    {
        _mockEventStore = new Mock<IEventStore>();
        _mockSnapshotStore = new Mock<ISnapshotStore>();
        _mockSnapshotStrategy = new Mock<ISnapshotStrategy>();

        _repository = new AggregateRepository<TestAggregate, Guid>(
            _mockEventStore.Object,
            _mockSnapshotStore.Object,
            _mockSnapshotStrategy.Object,
            null
        );
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldThrowAggregateNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();

        _mockSnapshotStore
            .Setup(x => x.GetLatestSnapshotAsync<Guid, TestAggregate>(id, "TestAggregate", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Snapshot<TestAggregate>?)null);

        _mockEventStore
            .Setup(x => x.GetEventsAsync(id, "TestAggregate", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<IEvent>());

        // Act
        var act = async () => await _repository.GetByIdAsync(id);

        // Assert
        await act.Should().ThrowAsync<AggregateNotFoundException>();
    }

    [Fact]
    public async Task SaveAsync_WithAggregateWithNoEvents_ShouldNotSaveAnything()
    {
        // Arrange
        var id = Guid.NewGuid();
        var aggregate = new TestAggregate();

        // Load from history first (empty history)
        aggregate.LoadFromHistory(Array.Empty<IEvent>());

        // Act
        await _repository.SaveAsync(aggregate);

        // Assert
        _mockEventStore.Verify(
            x => x.AppendEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IEnumerable<IEvent>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task SaveAsync_WithEventBus_ShouldPublishEvents()
    {
        // Arrange
        var mockEventBus = new Mock<IEventBus>();
        var repository = new AggregateRepository<TestAggregate, Guid>(
            _mockEventStore.Object,
            _mockSnapshotStore.Object,
            _mockSnapshotStrategy.Object,
            mockEventBus.Object
        );

        var id = Guid.NewGuid();
        var aggregate = new TestAggregate();
        aggregate.Create(id, "John", "john@example.com");

        _mockEventStore
            .Setup(x => x.AppendEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IEnumerable<IEvent>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockSnapshotStrategy
            .Setup(x => x.ShouldCreateSnapshot(It.IsAny<IAggregate<Guid>>(), It.IsAny<int>(), It.IsAny<DateTimeOffset?>()))
            .Returns(false);

        // Act
        await repository.SaveAsync(aggregate);

        // Assert
        mockEventBus.Verify(
            x => x.PublishAsync(It.IsAny<IEnumerable<IEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SaveAsync_WithoutEventBus_ShouldNotThrow()
    {
        // Arrange
        var repository = new AggregateRepository<TestAggregate, Guid>(
            _mockEventStore.Object,
            _mockSnapshotStore.Object,
            _mockSnapshotStrategy.Object,
            null // No event bus
        );

        var id = Guid.NewGuid();
        var aggregate = new TestAggregate();
        aggregate.Create(id, "John", "john@example.com");

        _mockEventStore
            .Setup(x => x.AppendEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IEnumerable<IEvent>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockSnapshotStrategy
            .Setup(x => x.ShouldCreateSnapshot(It.IsAny<IAggregate<Guid>>(), It.IsAny<int>(), It.IsAny<DateTimeOffset?>()))
            .Returns(false);

        // Act
        var act = async () => await repository.SaveAsync(aggregate);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveAsync_WhenSnapshotStrategyReturnsTrue_MultipleTimes_ShouldSaveSnapshot()
    {
        // Arrange
        var id = Guid.NewGuid();
        var aggregate = new TestAggregate();
        aggregate.Create(id, "John", "john@example.com");

        _mockEventStore
            .Setup(x => x.AppendEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IEnumerable<IEvent>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockSnapshotStrategy
            .Setup(x => x.ShouldCreateSnapshot(It.IsAny<IAggregate<Guid>>(), It.IsAny<int>(), It.IsAny<DateTimeOffset?>()))
            .Returns(true);

        _mockSnapshotStore
            .Setup(x => x.SaveSnapshotAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TestAggregate>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _repository.SaveAsync(aggregate);

        // First save
        aggregate.Rename("Jane");
        await _repository.SaveAsync(aggregate);

        // Second save
        aggregate.ChangeEmail("jane@example.com");
        await _repository.SaveAsync(aggregate);

        // Assert - should save snapshot 3 times (once for each save)
        _mockSnapshotStore.Verify(
            x => x.SaveSnapshotAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TestAggregate>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );
    }

    [Fact]
    public async Task GetByIdAsync_WithCancellationToken_ShouldPassToStores()
    {
        // Arrange
        var id = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Setup to return events so it doesn't throw AggregateNotFoundException
        _mockSnapshotStore
            .Setup(x => x.GetLatestSnapshotAsync<Guid, TestAggregate>(id, "TestAggregate", token))
            .ReturnsAsync((Snapshot<TestAggregate>?)null);

        _mockEventStore
            .Setup(x => x.GetEventsAsync(id, "TestAggregate", 0, token))
            .ReturnsAsync(new IEvent[] { new TestAggregateCreatedEvent(id, "Test", "test@example.com") });

        // Act
        await _repository.GetByIdAsync(id, token);

        // Assert
        _mockSnapshotStore.Verify(x => x.GetLatestSnapshotAsync<Guid, TestAggregate>(id, "TestAggregate", token), Times.Once);
        _mockEventStore.Verify(x => x.GetEventsAsync(id, "TestAggregate", 0, token), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_WhenEventStoreThrows_ShouldPropagateException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var aggregate = new TestAggregate();
        aggregate.Create(id, "John", "john@example.com");

        _mockEventStore
            .Setup(x => x.AppendEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IEnumerable<IEvent>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var act = async () => await _repository.SaveAsync(aggregate);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database error");
    }

    [Fact]
    public async Task GetByIdAsync_LoadingFromSnapshotAndEvents_ShouldReconstructCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Create a snapshot at version 5
        var snapshotAggregate = new TestAggregate();
        snapshotAggregate.LoadFromHistory(new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "John", "john@example.com"),
            new TestAggregateRenamedEvent("Jane"),
            new TestAggregateCounterIncrementedEvent(),
            new TestAggregateCounterIncrementedEvent(),
            new TestAggregateCounterIncrementedEvent()
        });

        var snapshot = new Snapshot<TestAggregate>(
            snapshotAggregate,
            5,
            DateTimeOffset.UtcNow
        );

        _mockSnapshotStore
            .Setup(x => x.GetLatestSnapshotAsync<Guid, TestAggregate>(id, "TestAggregate", It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Events after snapshot
        var subsequentEvents = new IEvent[]
        {
            new TestAggregateCounterIncrementedEvent(),
            new TestAggregateEmailChangedEvent("jane@example.com")
        };

        _mockEventStore
            .Setup(x => x.GetEventsAsync(id, "TestAggregate", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subsequentEvents);

        // Act
        var aggregate = await _repository.GetByIdAsync(id);

        // Assert
        aggregate.Id.Should().Be(id);
        aggregate.Version.Should().Be(7); // 5 from snapshot + 2 new events
        aggregate.Name.Should().Be("Jane");
        aggregate.Email.Should().Be("jane@example.com");
        aggregate.Counter.Should().Be(4); // 3 from snapshot + 1 new
    }
}
