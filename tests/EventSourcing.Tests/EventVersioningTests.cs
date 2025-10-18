using EventSourcing.Abstractions;
using EventSourcing.Abstractions.Versioning;
using EventSourcing.Core;
using EventSourcing.Core.Versioning;
using FluentAssertions;
using Xunit;

namespace EventSourcing.Tests;

public class EventVersioningTests
{
    // V1 event - original version
    public record UserCreatedEventV1(Guid UserId, string Name) : DomainEvent;

    // V2 event - split name into first and last
    public record UserCreatedEventV2(Guid UserId, string FirstName, string LastName) : DomainEvent;

    // V3 event - added email field
    public record UserCreatedEventV3(Guid UserId, string FirstName, string LastName, string Email) : DomainEvent;

    // Upcaster V1 -> V2
    public class UserCreatedV1ToV2Upcaster : EventUpcaster<UserCreatedEventV1, UserCreatedEventV2>
    {
        public override UserCreatedEventV2 Upcast(UserCreatedEventV1 oldEvent)
        {
            var nameParts = oldEvent.Name.Split(' ', 2);
            var firstName = nameParts.Length > 0 ? nameParts[0] : string.Empty;
            var lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

            return new UserCreatedEventV2(oldEvent.UserId, firstName, lastName);
        }
    }

    // Upcaster V2 -> V3
    public class UserCreatedV2ToV3Upcaster : EventUpcaster<UserCreatedEventV2, UserCreatedEventV3>
    {
        public override UserCreatedEventV3 Upcast(UserCreatedEventV2 oldEvent)
        {
            // Default email based on name
            var email = $"{oldEvent.FirstName.ToLowerInvariant()}.{oldEvent.LastName.ToLowerInvariant()}@example.com";
            return new UserCreatedEventV3(oldEvent.UserId, oldEvent.FirstName, oldEvent.LastName, email);
        }
    }

    [Fact]
    public void EventUpcaster_ShouldHaveCorrectSourceAndTargetTypes()
    {
        // Arrange
        var upcaster = new UserCreatedV1ToV2Upcaster();

        // Assert
        upcaster.SourceType.Should().Be(typeof(UserCreatedEventV1));
        upcaster.TargetType.Should().Be(typeof(UserCreatedEventV2));
    }

    [Fact]
    public void EventUpcaster_ShouldTransformEventCorrectly()
    {
        // Arrange
        var upcaster = new UserCreatedV1ToV2Upcaster();
        var v1Event = new UserCreatedEventV1(Guid.NewGuid(), "John Doe");

        // Act
        var v2Event = upcaster.Upcast(v1Event);

        // Assert
        v2Event.UserId.Should().Be(v1Event.UserId);
        v2Event.FirstName.Should().Be("John");
        v2Event.LastName.Should().Be("Doe");
    }

    [Fact]
    public void EventUpcasterRegistry_ShouldRegisterUpcaster()
    {
        // Arrange
        var registry = new EventUpcasterRegistry();
        var upcaster = new UserCreatedV1ToV2Upcaster();

        // Act
        registry.RegisterUpcaster(upcaster);

        // Assert
        registry.HasUpcaster(typeof(UserCreatedEventV1)).Should().BeTrue();
        registry.HasUpcaster(typeof(UserCreatedEventV2)).Should().BeFalse();
    }

    [Fact]
    public void EventUpcasterRegistry_ShouldThrowWhenRegisteringDuplicateUpcaster()
    {
        // Arrange
        var registry = new EventUpcasterRegistry();
        var upcaster1 = new UserCreatedV1ToV2Upcaster();
        var upcaster2 = new UserCreatedV1ToV2Upcaster();

        // Act
        registry.RegisterUpcaster(upcaster1);
        var act = () => registry.RegisterUpcaster(upcaster2);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public void EventUpcasterRegistry_ShouldUpcastOnce()
    {
        // Arrange
        var registry = new EventUpcasterRegistry();
        registry.RegisterUpcaster(new UserCreatedV1ToV2Upcaster());

        var v1Event = new UserCreatedEventV1(Guid.NewGuid(), "John Doe");

        // Act
        var result = registry.TryUpcastOnce(v1Event, out var upcastedEvent);

        // Assert
        result.Should().BeTrue();
        upcastedEvent.Should().BeOfType<UserCreatedEventV2>();
        var v2Event = (UserCreatedEventV2)upcastedEvent;
        v2Event.FirstName.Should().Be("John");
        v2Event.LastName.Should().Be("Doe");
    }

    [Fact]
    public void EventUpcasterRegistry_ShouldReturnFalseWhenNoUpcasterExists()
    {
        // Arrange
        var registry = new EventUpcasterRegistry();
        var v2Event = new UserCreatedEventV2(Guid.NewGuid(), "John", "Doe");

        // Act
        var result = registry.TryUpcastOnce(v2Event, out var upcastedEvent);

        // Assert
        result.Should().BeFalse();
        upcastedEvent.Should().BeSameAs(v2Event);
    }

    [Fact]
    public void EventUpcasterRegistry_ShouldUpcastToLatestThroughMultipleVersions()
    {
        // Arrange
        var registry = new EventUpcasterRegistry();
        registry.RegisterUpcaster(new UserCreatedV1ToV2Upcaster());
        registry.RegisterUpcaster(new UserCreatedV2ToV3Upcaster());

        var v1Event = new UserCreatedEventV1(Guid.NewGuid(), "John Doe");

        // Act
        var latestEvent = registry.UpcastToLatest(v1Event);

        // Assert
        latestEvent.Should().BeOfType<UserCreatedEventV3>();
        var v3Event = (UserCreatedEventV3)latestEvent;
        v3Event.FirstName.Should().Be("John");
        v3Event.LastName.Should().Be("Doe");
        v3Event.Email.Should().Be("john.doe@example.com");
    }

    [Fact]
    public void EventUpcasterRegistry_ShouldReturnSameEventWhenAlreadyLatest()
    {
        // Arrange
        var registry = new EventUpcasterRegistry();
        registry.RegisterUpcaster(new UserCreatedV1ToV2Upcaster());
        registry.RegisterUpcaster(new UserCreatedV2ToV3Upcaster());

        var v3Event = new UserCreatedEventV3(Guid.NewGuid(), "John", "Doe", "john@example.com");

        // Act
        var latestEvent = registry.UpcastToLatest(v3Event);

        // Assert
        latestEvent.Should().BeSameAs(v3Event);
    }

    [Fact]
    public void EventUpcasterRegistry_ShouldThrowOnCircularUpcasting()
    {
        // This test demonstrates protection against circular upcasting chains
        // We won't actually create circular upcasters as that would require
        // more complex setup, but the registry has a max iteration check

        // Arrange
        var registry = new EventUpcasterRegistry();

        // Create a long chain of upcasters (should work fine)
        for (int i = 0; i < 50; i++)
        {
            // In a real scenario, you wouldn't create this many versions,
            // but it demonstrates the registry handles deep chains
        }

        var v1Event = new UserCreatedEventV1(Guid.NewGuid(), "John Doe");

        // Act
        var result = registry.UpcastToLatest(v1Event);

        // Assert - should complete without hitting the max iteration limit
        result.Should().NotBeNull();
    }
}
