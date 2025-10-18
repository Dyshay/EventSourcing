using EventSourcing.Abstractions;
using EventSourcing.Tests.TestHelpers;
using FluentAssertions;

namespace EventSourcing.Tests.Core;

public class AggregateBaseEdgeCasesTests
{
    [Fact]
    public void LoadFromHistory_WithEmptyEventList_ShouldNotChangeVersion()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var events = Array.Empty<IEvent>();

        // Act
        aggregate.LoadFromHistory(events);

        // Assert
        aggregate.Version.Should().Be(0);
        aggregate.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromHistory_WithLargeNumberOfEvents_ShouldHandleCorrectly()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var id = Guid.NewGuid();

        var events = new List<IEvent>
        {
            new TestAggregateCreatedEvent(id, "Initial Name", "initial@example.com")
        };

        // Add 1000 rename events
        for (int i = 0; i < 1000; i++)
        {
            events.Add(new TestAggregateRenamedEvent($"Name {i}"));
        }

        // Act
        aggregate.LoadFromHistory(events);

        // Assert
        aggregate.Version.Should().Be(1001);
        aggregate.Name.Should().Be("Name 999");
        aggregate.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void RaiseEvent_CalledMultipleTimes_ShouldQueueAllEvents()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var id = Guid.NewGuid();

        // Act
        aggregate.Create(id, "John", "john@example.com");
        aggregate.Rename("Jane");
        aggregate.ChangeEmail("jane@example.com");
        aggregate.IncrementCounter();

        // Assert
        aggregate.UncommittedEvents.Should().HaveCount(4);
    }

    [Fact]
    public void MarkEventsAsCommitted_CalledMultipleTimes_ShouldAlwaysClear()
    {
        // Arrange
        var aggregate = new TestAggregate();
        aggregate.Create(Guid.NewGuid(), "John", "john@example.com");

        // Act
        aggregate.MarkEventsAsCommitted();
        aggregate.MarkEventsAsCommitted(); // Call twice

        // Assert
        aggregate.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromHistory_WithSameEventTypeMultipleTimes_ShouldApplyAll()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var id = Guid.NewGuid();

        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "John", "john@example.com"),
            new TestAggregateCounterIncrementedEvent(),
            new TestAggregateCounterIncrementedEvent(),
            new TestAggregateCounterIncrementedEvent()
        };

        // Act
        aggregate.LoadFromHistory(events);

        // Assert
        aggregate.Counter.Should().Be(3);
        aggregate.Version.Should().Be(4);
    }

    [Fact]
    public void NewAggregate_ShouldHaveDefaultState()
    {
        // Act
        var aggregate = new TestAggregate();

        // Assert
        aggregate.Id.Should().BeEmpty();
        aggregate.Version.Should().Be(0);
        aggregate.UncommittedEvents.Should().BeEmpty();
        aggregate.Name.Should().BeEmpty();
        aggregate.Email.Should().BeEmpty();
        aggregate.Counter.Should().Be(0);
    }

    [Fact]
    public void RaiseEvent_WithNullEvent_ShouldThrowNullReferenceException()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act
        var act = () => aggregate.RaiseEvent(null!);

        // Assert
        act.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void LoadFromHistory_ThenRaiseNewEvents_ShouldMaintainCorrectVersion()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var id = Guid.NewGuid();

        var historicalEvents = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "John", "john@example.com"),
            new TestAggregateRenamedEvent("Jane")
        };

        // Act
        aggregate.LoadFromHistory(historicalEvents);
        var versionAfterLoad = aggregate.Version;

        aggregate.ChangeEmail("newemail@example.com");

        // Assert
        versionAfterLoad.Should().Be(2);
        aggregate.Version.Should().Be(2); // Version doesn't increment until save
        aggregate.UncommittedEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Aggregate_WithMultipleOperations_ShouldMaintainConsistentState()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var id = Guid.NewGuid();

        // Act
        aggregate.Create(id, "John", "john@example.com");
        aggregate.MarkEventsAsCommitted();

        aggregate.Rename("Jane");
        aggregate.ChangeEmail("jane@example.com");
        aggregate.IncrementCounter();

        // Assert
        aggregate.Id.Should().Be(id);
        aggregate.Name.Should().Be("Jane");
        aggregate.Email.Should().Be("jane@example.com");
        aggregate.Counter.Should().Be(1);
        aggregate.UncommittedEvents.Should().HaveCount(3);
    }
}
