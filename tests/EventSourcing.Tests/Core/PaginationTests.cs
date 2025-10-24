using EventSourcing.Abstractions;
using EventSourcing.Core;
using EventSourcing.Core.Snapshots;
using EventSourcing.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace EventSourcing.Tests.Core;

public class PaginationTests
{
    private readonly Mock<IEventStore> _eventStoreMock;
    private readonly Mock<ISnapshotStore> _snapshotStoreMock;
    private readonly Mock<ISnapshotStrategy> _snapshotStrategyMock;
    private readonly AggregateRepository<TestAggregate, Guid> _repository;

    public PaginationTests()
    {
        _eventStoreMock = new Mock<IEventStore>();
        _snapshotStoreMock = new Mock<ISnapshotStore>();
        _snapshotStrategyMock = new Mock<ISnapshotStrategy>();

        _repository = new AggregateRepository<TestAggregate, Guid>(
            _eventStoreMock.Object,
            _snapshotStoreMock.Object,
            _snapshotStrategyMock.Object);
    }

    [Fact]
    public async Task GetAllPaginatedAsync_ShouldReturnPaginatedResults()
    {
        // Arrange
        var guid1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var guid2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var aggregateIds = new List<string> { guid1.ToString(), guid2.ToString() };
        var paginatedIds = new PagedResult<string>(
            aggregateIds,
            pageNumber: 1,
            pageSize: 2,
            totalCount: 5);

        _eventStoreMock
            .Setup(e => e.GetAggregateIdsPaginatedAsync("TestAggregate", 1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedIds);

        // Setup individual aggregate loading
        var aggregate1 = CreateTestAggregate(guid1);
        var aggregate2 = CreateTestAggregate(guid2);

        SetupAggregateLoading(aggregate1);
        SetupAggregateLoading(aggregate2);

        // Act
        var result = await _repository.GetAllPaginatedAsync(1, 2);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(2);
        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(3);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllPaginatedAsync_ShouldReturnEmptyResult_WhenNoAggregates()
    {
        // Arrange
        var paginatedIds = PagedResult<string>.Empty(1, 10);

        _eventStoreMock
            .Setup(e => e.GetAggregateIdsPaginatedAsync("TestAggregate", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedIds);

        // Act
        var result = await _repository.GetAllPaginatedAsync(1, 10);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllPaginatedAsync_ShouldSkipCorruptedAggregates()
    {
        // Arrange
        var guid1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var guid3 = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var aggregateIds = new List<string> { guid1.ToString(), "corrupt-id", guid3.ToString() };
        var paginatedIds = new PagedResult<string>(
            aggregateIds,
            pageNumber: 1,
            pageSize: 10,
            totalCount: 3);

        _eventStoreMock
            .Setup(e => e.GetAggregateIdsPaginatedAsync("TestAggregate", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedIds);

        // Setup valid aggregates
        var aggregate1 = CreateTestAggregate(guid1);
        var aggregate3 = CreateTestAggregate(guid3);

        SetupAggregateLoading(aggregate1);
        SetupAggregateLoading(aggregate3);

        // Setup corrupted aggregate to throw exception
        _snapshotStoreMock
            .Setup(s => s.GetLatestSnapshotAsync<Guid, TestAggregate>(
                It.Is<Guid>(id => id.ToString().Contains("corrupt")),
                "TestAggregate",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AggregateNotFoundException("Corrupted aggregate"));

        // Act
        var result = await _repository.GetAllPaginatedAsync(1, 10);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2); // Only valid aggregates
        result.TotalCount.Should().Be(3); // Original count maintained
    }

    private TestAggregate CreateTestAggregate(Guid id)
    {
        var aggregate = new TestAggregate();
        aggregate.Create(id, "Test Name", "test@example.com");
        aggregate.MarkEventsAsCommitted();
        return aggregate;
    }

    private void SetupAggregateLoading(TestAggregate aggregate)
    {
        _snapshotStoreMock
            .Setup(s => s.GetLatestSnapshotAsync<Guid, TestAggregate>(
                aggregate.Id,
                "TestAggregate",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Snapshot<TestAggregate>?)null);

        _eventStoreMock
            .Setup(e => e.GetEventsAsync<Guid>(
                aggregate.Id,
                "TestAggregate",
                0,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IEvent>
            {
                new TestAggregateCreatedEvent(aggregate.Id, aggregate.Name, aggregate.Email)
            });
    }
}