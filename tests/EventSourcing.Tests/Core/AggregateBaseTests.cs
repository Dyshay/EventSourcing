using EventSourcing.Abstractions;
using EventSourcing.Tests.TestHelpers;
using FluentAssertions;

namespace EventSourcing.Tests.Core;

public class AggregateBaseTests
{
    [Fact]
    public void RaiseEvent_ShouldApplyEventAndAddToUncommittedEvents()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var aggregateId = Guid.NewGuid();

        // Act
        aggregate.Create(aggregateId, "John Doe", "john@example.com");

        // Assert
        aggregate.Id.Should().Be(aggregateId);
        aggregate.Name.Should().Be("John Doe");
        aggregate.Email.Should().Be("john@example.com");
        aggregate.UncommittedEvents.Should().HaveCount(1);
        aggregate.UncommittedEvents[0].Should().BeOfType<TestAggregateCreatedEvent>();
    }

    [Fact]
    public void RaiseEvent_MultipleEvents_ShouldApplyAllEventsInOrder()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var aggregateId = Guid.NewGuid();

        // Act
        aggregate.Create(aggregateId, "John Doe", "john@example.com");
        aggregate.Rename("Jane Doe");
        aggregate.ChangeEmail("jane@example.com");

        // Assert
        aggregate.Name.Should().Be("Jane Doe");
        aggregate.Email.Should().Be("jane@example.com");
        aggregate.UncommittedEvents.Should().HaveCount(3);
    }

    [Fact]
    public void LoadFromHistory_ShouldReplayEventsAndIncrementVersion()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var aggregateId = Guid.NewGuid();
        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(aggregateId, "John Doe", "john@example.com"),
            new TestAggregateRenamedEvent("Jane Doe"),
            new TestAggregateEmailChangedEvent("jane@example.com")
        };

        // Act
        aggregate.LoadFromHistory(events);

        // Assert
        aggregate.Id.Should().Be(aggregateId);
        aggregate.Name.Should().Be("Jane Doe");
        aggregate.Email.Should().Be("jane@example.com");
        aggregate.Version.Should().Be(3);
        aggregate.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void MarkEventsAsCommitted_ShouldClearUncommittedEvents()
    {
        // Arrange
        var aggregate = new TestAggregate();
        aggregate.Create(Guid.NewGuid(), "John Doe", "john@example.com");
        aggregate.Rename("Jane Doe");

        // Act
        aggregate.MarkEventsAsCommitted();

        // Assert
        aggregate.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void Version_ShouldIncrement_WhenLoadingFromHistory()
    {
        // Arrange
        var aggregate = new TestAggregate();
        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(Guid.NewGuid(), "Test", "test@example.com"),
            new TestAggregateCounterIncrementedEvent(),
            new TestAggregateCounterIncrementedEvent(),
            new TestAggregateCounterIncrementedEvent()
        };

        // Act
        aggregate.LoadFromHistory(events);

        // Assert
        aggregate.Version.Should().Be(4);
        aggregate.Counter.Should().Be(3);
    }

    [Fact]
    public void ApplyEvents_ShouldNotAffectVersion_WhenRaisingNewEvents()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act
        aggregate.Create(Guid.NewGuid(), "Test", "test@example.com");
        aggregate.IncrementCounter();
        aggregate.IncrementCounter();

        // Assert
        aggregate.Version.Should().Be(0); // Version only changes via LoadFromHistory or when repository saves
        aggregate.Counter.Should().Be(2);
        aggregate.UncommittedEvents.Should().HaveCount(3);
    }
}
