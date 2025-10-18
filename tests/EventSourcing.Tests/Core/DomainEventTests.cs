using EventSourcing.Tests.TestHelpers;
using FluentAssertions;

namespace EventSourcing.Tests.Core;

public class DomainEventTests
{
    [Fact]
    public void DomainEvent_ShouldAutoGenerateEventId()
    {
        // Act
        var event1 = new TestAggregateCreatedEvent(Guid.NewGuid(), "John", "john@example.com");
        var event2 = new TestAggregateCreatedEvent(Guid.NewGuid(), "Jane", "jane@example.com");

        // Assert
        event1.EventId.Should().NotBeEmpty();
        event2.EventId.Should().NotBeEmpty();
        event1.EventId.Should().NotBe(event2.EventId);
    }

    [Fact]
    public void DomainEvent_ShouldAutoGenerateTimestamp()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var @event = new TestAggregateCreatedEvent(Guid.NewGuid(), "John", "john@example.com");

        // Assert
        var after = DateTimeOffset.UtcNow;
        @event.Timestamp.Should().BeOnOrAfter(before);
        @event.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void DomainEvent_ShouldGenerateEventTypeFromClassName()
    {
        // Act
        var @event = new TestAggregateCreatedEvent(Guid.NewGuid(), "John", "john@example.com");

        // Assert
        @event.EventType.Should().Be("TestAggregateCreatedEvent");
    }

    [Fact]
    public void DomainEvent_DifferentEventTypes_ShouldHaveDifferentEventTypeNames()
    {
        // Act
        var createdEvent = new TestAggregateCreatedEvent(Guid.NewGuid(), "John", "john@example.com");
        var renamedEvent = new TestAggregateRenamedEvent("Jane");

        // Assert
        createdEvent.EventType.Should().NotBe(renamedEvent.EventType);
        createdEvent.EventType.Should().Be("TestAggregateCreatedEvent");
        renamedEvent.EventType.Should().Be("TestAggregateRenamedEvent");
    }

    [Fact]
    public void DomainEvent_Timestamp_ShouldBeUtc()
    {
        // Act
        var @event = new TestAggregateCreatedEvent(Guid.NewGuid(), "John", "john@example.com");

        // Assert
        @event.Timestamp.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void DomainEvent_CreatedCloseInTime_ShouldHaveCloseTimestamps()
    {
        // Act
        var event1 = new TestAggregateCreatedEvent(Guid.NewGuid(), "John", "john@example.com");
        var event2 = new TestAggregateCreatedEvent(Guid.NewGuid(), "Jane", "jane@example.com");

        // Assert
        (event2.Timestamp - event1.Timestamp).Should().BeLessThan(TimeSpan.FromSeconds(1));
    }
}
