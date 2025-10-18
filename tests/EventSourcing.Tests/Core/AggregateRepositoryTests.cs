using EventSourcing.Abstractions;
using EventSourcing.Core;
using EventSourcing.Core.Publishing;
using EventSourcing.Core.Snapshots;
using EventSourcing.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace EventSourcing.Tests.Core;

public class AggregateRepositoryTests
{
    private readonly Mock<IEventStore> _eventStoreMock;
    private readonly Mock<ISnapshotStore> _snapshotStoreMock;
    private readonly Mock<ISnapshotStrategy> _snapshotStrategyMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly AggregateRepository<TestAggregate, Guid> _repository;

    public AggregateRepositoryTests()
    {
        _eventStoreMock = new Mock<IEventStore>();
        _snapshotStoreMock = new Mock<ISnapshotStore>();
        _snapshotStrategyMock = new Mock<ISnapshotStrategy>();
        _eventBusMock = new Mock<IEventBus>();

        _repository = new AggregateRepository<TestAggregate, Guid>(
            _eventStoreMock.Object,
            _snapshotStoreMock.Object,
            _snapshotStrategyMock.Object,
            _eventBusMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldLoadFromEventsOnly_WhenNoSnapshotExists()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var events = new List<IEvent>
        {
            new TestAggregateCreatedEvent(aggregateId, "John Doe", "john@example.com"),
            new TestAggregateRenamedEvent("Jane Doe")
        };

        _snapshotStoreMock
            .Setup(s => s.GetLatestSnapshotAsync<Guid, TestAggregate>(
                aggregateId, "TestAggregate", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Snapshot<TestAggregate>?)null);

        _eventStoreMock
            .Setup(e => e.GetEventsAsync<Guid>(
                aggregateId, "TestAggregate", 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Act
        var aggregate = await _repository.GetByIdAsync(aggregateId);

        // Assert
        aggregate.Id.Should().Be(aggregateId);
        aggregate.Name.Should().Be("Jane Doe");
        aggregate.Version.Should().Be(2);
        aggregate.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldLoadFromSnapshot_ThenReplaySubsequentEvents()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var snapshotAggregate = new TestAggregate();
        snapshotAggregate.LoadFromHistory(new IEvent[]
        {
            new TestAggregateCreatedEvent(aggregateId, "John Doe", "john@example.com"),
            new TestAggregateRenamedEvent("Jane Doe")
        });

        var snapshot = new Snapshot<TestAggregate>(snapshotAggregate, 2, DateTimeOffset.UtcNow);
        var subsequentEvents = new List<IEvent>
        {
            new TestAggregateEmailChangedEvent("jane.new@example.com")
        };

        _snapshotStoreMock
            .Setup(s => s.GetLatestSnapshotAsync<Guid, TestAggregate>(
                aggregateId, "TestAggregate", It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        _eventStoreMock
            .Setup(e => e.GetEventsAsync<Guid>(
                aggregateId, "TestAggregate", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subsequentEvents);

        // Act
        var aggregate = await _repository.GetByIdAsync(aggregateId);

        // Assert
        aggregate.Name.Should().Be("Jane Doe");
        aggregate.Email.Should().Be("jane.new@example.com");
        aggregate.Version.Should().Be(3);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrow_WhenAggregateDoesNotExist()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();

        _snapshotStoreMock
            .Setup(s => s.GetLatestSnapshotAsync<Guid, TestAggregate>(
                aggregateId, "TestAggregate", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Snapshot<TestAggregate>?)null);

        _eventStoreMock
            .Setup(e => e.GetEventsAsync<Guid>(
                aggregateId, "TestAggregate", 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IEvent>());

        // Act
        var act = async () => await _repository.GetByIdAsync(aggregateId);

        // Assert
        await act.Should().ThrowAsync<AggregateNotFoundException>();
    }

    [Fact]
    public async Task SaveAsync_ShouldAppendEventsAndPublishToEventBus()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var aggregateId = Guid.NewGuid();
        aggregate.Create(aggregateId, "John Doe", "john@example.com");
        aggregate.Rename("Jane Doe");

        _snapshotStoreMock
            .Setup(s => s.GetLatestSnapshotAsync<Guid, TestAggregate>(
                aggregateId, "TestAggregate", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Snapshot<TestAggregate>?)null);

        _snapshotStrategyMock
            .Setup(s => s.ShouldCreateSnapshot(aggregate, 2, null))
            .Returns(false);

        // Act
        await _repository.SaveAsync(aggregate);

        // Assert
        _eventStoreMock.Verify(
            e => e.AppendEventsAsync(
                aggregateId,
                "TestAggregate",
                It.Is<IEnumerable<IEvent>>(events => events.Count() == 2),
                0,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _eventBusMock.Verify(
            b => b.PublishAsync(
                It.Is<IEnumerable<IEvent>>(events => events.Count() == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);

        aggregate.UncommittedEvents.Should().BeEmpty();
        aggregate.Version.Should().Be(2);
    }

    [Fact]
    public async Task SaveAsync_ShouldCreateSnapshot_WhenStrategyIndicates()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var aggregateId = Guid.NewGuid();
        aggregate.Create(aggregateId, "John Doe", "john@example.com");

        _snapshotStoreMock
            .Setup(s => s.GetLatestSnapshotAsync<Guid, TestAggregate>(
                aggregateId, "TestAggregate", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Snapshot<TestAggregate>?)null);

        _snapshotStrategyMock
            .Setup(s => s.ShouldCreateSnapshot(It.IsAny<TestAggregate>(), 1, null))
            .Returns(true);

        // Act
        await _repository.SaveAsync(aggregate);

        // Assert
        _snapshotStoreMock.Verify(
            s => s.SaveSnapshotAsync(
                aggregateId,
                "TestAggregate",
                It.IsAny<TestAggregate>(),
                1,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveAsync_ShouldNotSaveAnything_WhenNoUncommittedEvents()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act
        await _repository.SaveAsync(aggregate);

        // Assert
        _eventStoreMock.Verify(
            e => e.AppendEventsAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<IEvent>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenAggregateExists()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var events = new List<IEvent>
        {
            new TestAggregateCreatedEvent(aggregateId, "Test", "test@example.com")
        };

        _snapshotStoreMock
            .Setup(s => s.GetLatestSnapshotAsync<Guid, TestAggregate>(
                aggregateId, "TestAggregate", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Snapshot<TestAggregate>?)null);

        _eventStoreMock
            .Setup(e => e.GetEventsAsync<Guid>(
                aggregateId, "TestAggregate", 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Act
        var exists = await _repository.ExistsAsync(aggregateId);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenAggregateDoesNotExist()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();

        _snapshotStoreMock
            .Setup(s => s.GetLatestSnapshotAsync<Guid, TestAggregate>(
                aggregateId, "TestAggregate", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Snapshot<TestAggregate>?)null);

        _eventStoreMock
            .Setup(e => e.GetEventsAsync<Guid>(
                aggregateId, "TestAggregate", 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IEvent>());

        // Act
        var exists = await _repository.ExistsAsync(aggregateId);

        // Assert
        exists.Should().BeFalse();
    }
}
