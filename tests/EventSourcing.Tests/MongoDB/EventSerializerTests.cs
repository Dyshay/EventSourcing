using EventSourcing.Abstractions;
using EventSourcing.MongoDB.Serialization;
using EventSourcing.Tests.TestHelpers;
using FluentAssertions;

namespace EventSourcing.Tests.MongoDB;

public class EventSerializerTests
{
    private readonly EventSerializer _serializer;

    public EventSerializerTests()
    {
        _serializer = new EventSerializer();

        // Register test event types
        EventSerializer.RegisterEventType(typeof(TestAggregateCreatedEvent));
        EventSerializer.RegisterEventType(typeof(TestAggregateRenamedEvent));
        EventSerializer.RegisterEventType(typeof(TestAggregateEmailChangedEvent));
    }

    [Fact]
    public void Serialize_ShouldSerializeEventToJson()
    {
        // Arrange
        var @event = new TestAggregateCreatedEvent(Guid.NewGuid(), "John Doe", "john@example.com");

        // Act
        var json = _serializer.Serialize(@event);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("john@example.com");
        json.Should().Contain("John Doe");
    }

    [Fact]
    public void Deserialize_WithRegisteredType_ShouldDeserializeSuccessfully()
    {
        // Arrange
        var originalEvent = new TestAggregateCreatedEvent(Guid.NewGuid(), "John Doe", "john@example.com");
        var json = _serializer.Serialize(originalEvent);

        // Act
        var deserializedEvent = _serializer.Deserialize(originalEvent.EventType, json);

        // Assert
        deserializedEvent.Should().NotBeNull();
        deserializedEvent.Should().BeOfType<TestAggregateCreatedEvent>();

        var typedEvent = (TestAggregateCreatedEvent)deserializedEvent;
        typedEvent.Name.Should().Be("John Doe");
        typedEvent.Email.Should().Be("john@example.com");
        typedEvent.Id.Should().Be(originalEvent.Id);
    }

    [Fact]
    public void Deserialize_WithUnregisteredType_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var json = "{\"id\":\"123\"}";

        // Act
        var act = () => _serializer.Deserialize("UnregisteredEvent", json);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not registered*");
    }

    [Fact]
    public void RegisterEventType_WithValidEventType_ShouldRegisterSuccessfully()
    {
        // Arrange
        var eventType = typeof(TestAggregateCounterIncrementedEvent);

        // Act
        var act = () => EventSerializer.RegisterEventType(eventType);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterEventType_WithNonEventType_ShouldThrowArgumentException()
    {
        // Arrange
        var invalidType = typeof(string);

        // Act
        var act = () => EventSerializer.RegisterEventType(invalidType);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must implement IEvent*");
    }

    [Fact]
    public void SerializeDeserialize_PreservesEventId_AndTimestamp()
    {
        // Arrange
        var originalEvent = new TestAggregateCreatedEvent(Guid.NewGuid(), "Jane Doe", "jane@example.com");
        var originalEventId = originalEvent.EventId;
        var originalTimestamp = originalEvent.Timestamp;

        // Act
        var json = _serializer.Serialize(originalEvent);
        var deserializedEvent = _serializer.Deserialize(originalEvent.EventType, json) as TestAggregateCreatedEvent;

        // Assert
        deserializedEvent.Should().NotBeNull();
        deserializedEvent!.EventId.Should().Be(originalEventId);
        deserializedEvent.Timestamp.Should().BeCloseTo(originalTimestamp, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void Serialize_WithComplexEvent_ShouldSerializeAllProperties()
    {
        // Arrange
        var @event = new TestAggregateEmailChangedEvent("new@example.com");

        // Act
        var json = _serializer.Serialize(@event);

        // Assert
        json.Should().Contain("new@example.com");
        json.Should().Contain("eventId");
        json.Should().Contain("timestamp");
    }
}
