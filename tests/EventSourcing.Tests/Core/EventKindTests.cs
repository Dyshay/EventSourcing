using EventSourcing.Tests.TestHelpers;
using FluentAssertions;

namespace EventSourcing.Tests.Core;

public class EventKindTests
{
    [Fact]
    public void DomainEvent_ShouldAutoGenerateKind_FromEventTypeName()
    {
        // Act
        var createdEvent = new TestAggregateCreatedEvent(Guid.NewGuid(), "John", "john@example.com");

        // Assert - Format is "test.aggregatecreated" (first word is aggregate, rest is action)
        createdEvent.Kind.Should().Be("test.aggregatecreated");
    }

    [Fact]
    public void DomainEvent_WithDifferentEventTypes_ShouldGenerateDifferentKinds()
    {
        // Act
        var createdEvent = new TestAggregateCreatedEvent(Guid.NewGuid(), "John", "john@example.com");
        var renamedEvent = new TestAggregateRenamedEvent("Jane");
        var emailChangedEvent = new TestAggregateEmailChangedEvent("new@example.com");
        var activatedEvent = new TestAggregateCounterIncrementedEvent();

        // Assert
        createdEvent.Kind.Should().Be("test.aggregatecreated");
        renamedEvent.Kind.Should().Be("test.aggregaterenamed");
        emailChangedEvent.Kind.Should().Be("test.aggregateemailchanged");
        activatedEvent.Kind.Should().Be("test.aggregatecounterincremented");
    }

    [Fact]
    public void DomainEvent_Kind_ShouldBeLowercase()
    {
        // Act
        var @event = new TestAggregateCreatedEvent(Guid.NewGuid(), "John", "john@example.com");

        // Assert
        @event.Kind.Should().MatchRegex("^[a-z.]+$");
    }

    [Fact]
    public void DomainEvent_Kind_ShouldFollowAggregateActionPattern()
    {
        // Act
        var @event = new TestAggregateCreatedEvent(Guid.NewGuid(), "John", "john@example.com");

        // Assert - Format should be "aggregate.action"
        @event.Kind.Should().Contain(".");
        var parts = @event.Kind.Split('.');
        parts.Should().HaveCount(2);
        parts[0].Should().Be("test"); // aggregate name
        parts[1].Should().Be("aggregatecreated"); // action
    }

    [Fact]
    public void MultipleEvents_ShouldHaveUniqueKinds()
    {
        // Act
        var events = new[]
        {
            new TestAggregateCreatedEvent(Guid.NewGuid(), "John", "john@example.com").Kind,
            new TestAggregateRenamedEvent("Jane").Kind,
            new TestAggregateEmailChangedEvent("new@example.com").Kind,
            new TestAggregateCounterIncrementedEvent().Kind
        };

        // Assert - All kinds should be unique
        events.Distinct().Should().HaveCount(4);
    }
}
