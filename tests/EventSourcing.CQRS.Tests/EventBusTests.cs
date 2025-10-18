using EventSourcing.Abstractions;
using EventSourcing.Core;
using EventSourcing.CQRS.Events;
using EventSourcing.CQRS.Queries;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace EventSourcing.CQRS.Tests;

public class EventBusTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly TestEventHandler _eventHandler;
    private readonly IQueryCache _mockCache;
    private readonly IEventStreamPublisher _mockStreamPublisher;

    public EventBusTests()
    {
        _eventHandler = new TestEventHandler();
        _mockCache = Substitute.For<IQueryCache>();
        _mockStreamPublisher = Substitute.For<IEventStreamPublisher>();

        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<TestDomainEvent>>(_eventHandler);
        services.AddSingleton<IQueryCache>(_mockCache);
        services.AddSingleton<IEventStreamPublisher>(_mockStreamPublisher);
        services.AddSingleton<IEventBus>(sp =>
            new EventBus(
                sp,
                NullLogger<EventBus>.Instance,
                sp.GetService<IQueryCache>(),
                sp.GetService<IEventStreamPublisher>()));

        _serviceProvider = services.BuildServiceProvider();
        _eventBus = _serviceProvider.GetRequiredService<IEventBus>();
    }

    [Fact]
    public async Task PublishAsync_WithRegisteredHandler_ShouldInvokeHandler()
    {
        // Arrange
        var @event = new TestDomainEvent { Value = "test" };

        // Act
        await _eventBus.PublishAsync(@event);

        // Assert
        _eventHandler.HandledEvents.Should().ContainSingle();
        _eventHandler.HandledEvents[0].Value.Should().Be("test");
    }

    [Fact]
    public async Task PublishAsync_ShouldInvalidateCache()
    {
        // Arrange
        var @event = new TestDomainEvent { Value = "test" };

        // Act
        await _eventBus.PublishAsync(@event);

        // Assert
        await _mockCache.Received(1).InvalidateByEventAsync(
            @event.EventType,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_WithMultipleHandlers_ShouldInvokeAllHandlers()
    {
        // Arrange
        var handler1 = new TestEventHandler();
        var handler2 = new TestEventHandler();

        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<TestDomainEvent>>(handler1);
        services.AddSingleton<IEventHandler<TestDomainEvent>>(handler2);
        services.AddSingleton<IEventBus>(sp =>
            new EventBus(sp, NullLogger<EventBus>.Instance));

        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var @event = new TestDomainEvent { Value = "test" };

        // Act
        await eventBus.PublishAsync(@event);

        // Assert
        handler1.HandledEvents.Should().ContainSingle();
        handler2.HandledEvents.Should().ContainSingle();
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldPublishAllEvents()
    {
        // Arrange
        var events = new[]
        {
            new TestDomainEvent { Value = "event1" },
            new TestDomainEvent { Value = "event2" },
            new TestDomainEvent { Value = "event3" }
        };

        // Act
        await _eventBus.PublishBatchAsync(events);

        // Assert
        _eventHandler.HandledEvents.Should().HaveCount(3);
        _eventHandler.HandledEvents[0].Value.Should().Be("event1");
        _eventHandler.HandledEvents[1].Value.Should().Be("event2");
        _eventHandler.HandledEvents[2].Value.Should().Be("event3");
    }

    [Fact]
    public async Task PublishToStreamAsync_WithConfiguredPublisher_ShouldPublishToStream()
    {
        // Arrange
        var @event = new TestDomainEvent { Value = "test" };

        // Act
        await _eventBus.PublishToStreamAsync(@event);

        // Assert
        await _mockStreamPublisher.Received(1).PublishAsync(
            @event,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishToStreamAsync_WithoutConfiguredPublisher_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(sp =>
            new EventBus(sp, NullLogger<EventBus>.Instance));

        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var @event = new TestDomainEvent { Value = "test" };

        // Act
        Func<Task> act = async () => await eventBus.PublishToStreamAsync(@event);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishBatchToStreamAsync_WithConfiguredPublisher_ShouldPublishToStream()
    {
        // Arrange
        var events = new[]
        {
            new TestDomainEvent { Value = "event1" },
            new TestDomainEvent { Value = "event2" }
        };

        // Act
        await _eventBus.PublishBatchToStreamAsync(events);

        // Assert
        await _mockStreamPublisher.Received(1).PublishBatchAsync(
            Arg.Is<IEnumerable<IEvent>>(e => e.Count() == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishBatchToStreamAsync_WithoutConfiguredPublisher_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(sp =>
            new EventBus(sp, NullLogger<EventBus>.Instance));

        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var events = new[]
        {
            new TestDomainEvent { Value = "event1" },
            new TestDomainEvent { Value = "event2" }
        };

        // Act
        Func<Task> act = async () => await eventBus.PublishBatchToStreamAsync(events);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithHandlerException_ShouldPropagateException()
    {
        // Arrange
        var failingHandler = new FailingEventHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<TestDomainEvent>>(failingHandler);
        services.AddSingleton<IEventBus>(sp =>
            new EventBus(sp, NullLogger<EventBus>.Instance));

        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();

        var @event = new TestDomainEvent { Value = "test" };

        // Act
        Func<Task> act = async () => await eventBus.PublishAsync(@event);

        // Assert
        // EventBus uses reflection which wraps exceptions in TargetInvocationException
        await act.Should().ThrowAsync<System.Reflection.TargetInvocationException>();
    }
}

// Test types
public record TestDomainEvent : DomainEvent
{
    public string Value { get; init; } = string.Empty;
}

public class TestEventHandler : IEventHandler<TestDomainEvent>
{
    public List<TestDomainEvent> HandledEvents { get; } = new();

    public Task HandleAsync(TestDomainEvent @event, CancellationToken cancellationToken = default)
    {
        HandledEvents.Add(@event);
        return Task.CompletedTask;
    }
}

public class FailingEventHandler : IEventHandler<TestDomainEvent>
{
    public Task HandleAsync(TestDomainEvent @event, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Handler failed");
    }
}
