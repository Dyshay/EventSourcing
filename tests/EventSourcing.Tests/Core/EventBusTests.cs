using EventSourcing.Abstractions;
using EventSourcing.Core.Projections;
using EventSourcing.Core.Publishing;
using EventSourcing.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace EventSourcing.Tests.Core;

public class EventBusTests
{
    [Fact]
    public async Task PublishAsync_ShouldInvokeProjectionHandlers()
    {
        // Arrange
        var projection = new TestProjection();
        var services = new ServiceCollection();
        services.AddSingleton<IProjection>(projection);
        var serviceProvider = services.BuildServiceProvider();

        var eventBus = new EventBus(serviceProvider);
        var @event = new TestAggregateCreatedEvent(Guid.NewGuid(), "Test", "test@example.com");

        // Act
        await eventBus.PublishAsync(@event);

        // Assert
        projection.CreatedEvents.Should().ContainSingle();
        projection.CreatedEvents[0].Should().Be(@event);
    }

    [Fact]
    public async Task PublishAsync_ShouldInvokeMultipleProjections()
    {
        // Arrange
        var projection1 = new TestProjection();
        var projection2 = new TestProjection();
        var services = new ServiceCollection();
        services.AddSingleton<IProjection>(projection1);
        services.AddSingleton<IProjection>(projection2);
        var serviceProvider = services.BuildServiceProvider();

        var eventBus = new EventBus(serviceProvider);
        var @event = new TestAggregateCreatedEvent(Guid.NewGuid(), "Test", "test@example.com");

        // Act
        await eventBus.PublishAsync(@event);

        // Assert
        projection1.CreatedEvents.Should().ContainSingle();
        projection2.CreatedEvents.Should().ContainSingle();
    }

    [Fact]
    public async Task PublishAsync_ShouldPublishToExternalPublishers()
    {
        // Arrange
        var publisherMock = new Mock<IEventPublisher>();
        var services = new ServiceCollection();
        services.AddSingleton(publisherMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var eventBus = new EventBus(serviceProvider);
        var @event = new TestAggregateCreatedEvent(Guid.NewGuid(), "Test", "test@example.com");

        // Act
        await eventBus.PublishAsync(@event);

        // Assert
        publisherMock.Verify(
            p => p.PublishAsync(@event, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WithMultipleEvents_ShouldPublishAll()
    {
        // Arrange
        var projection = new TestProjection();
        var services = new ServiceCollection();
        services.AddSingleton<IProjection>(projection);
        var serviceProvider = services.BuildServiceProvider();

        var eventBus = new EventBus(serviceProvider);
        var events = new List<IEvent>
        {
            new TestAggregateCreatedEvent(Guid.NewGuid(), "Test1", "test1@example.com"),
            new TestAggregateRenamedEvent("Test2"),
            new TestAggregateCreatedEvent(Guid.NewGuid(), "Test3", "test3@example.com")
        };

        // Act
        await eventBus.PublishAsync(events);

        // Assert
        projection.CreatedEvents.Should().HaveCount(2);
        projection.RenamedEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task PublishAsync_WithNoProjections_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var eventBus = new EventBus(serviceProvider);
        var @event = new TestAggregateCreatedEvent(Guid.NewGuid(), "Test", "test@example.com");

        // Act
        var act = async () => await eventBus.PublishAsync(@event);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithEmptyEventList_ShouldNotInvokeHandlers()
    {
        // Arrange
        var projection = new TestProjection();
        var services = new ServiceCollection();
        services.AddSingleton<IProjection>(projection);
        var serviceProvider = services.BuildServiceProvider();

        var eventBus = new EventBus(serviceProvider);

        // Act
        await eventBus.PublishAsync(new List<IEvent>());

        // Assert
        projection.CreatedEvents.Should().BeEmpty();
        projection.RenamedEvents.Should().BeEmpty();
    }
}
