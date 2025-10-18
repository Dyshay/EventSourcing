using EventSourcing.Abstractions;
using EventSourcing.Core;
using EventSourcing.CQRS.Commands;
using EventSourcing.CQRS.Configuration;
using EventSourcing.CQRS.Context;
using EventSourcing.CQRS.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventSourcing.CQRS.Tests;

public class CommandBusTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandBus _commandBus;
    private readonly ICommandContextAccessor _contextAccessor;

    public CommandBusTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(cqrs =>
        {
            cqrs.AddCommandHandler<TestCommand, TestEvent, TestCommandHandler>();
        });

        // Register multi-event handler manually
        services.AddTransient<ICommandHandlerMultiEvent<TestMultiEventCommand>, TestMultiEventCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _commandBus = _serviceProvider.GetRequiredService<ICommandBus>();
        _contextAccessor = _serviceProvider.GetRequiredService<ICommandContextAccessor>();
    }

    [Fact]
    public async Task SendAsync_WithValidCommand_ShouldReturnSuccessResult()
    {
        // Arrange
        var command = new TestCommand { Value = "test" };

        // Act
        var result = await _commandBus.SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Value.Should().Be("test");
    }

    [Fact]
    public async Task SendAsync_WithValidCommand_ShouldSetCommandContext()
    {
        // Arrange
        var command = new TestCommand { Value = "test" };

        // Act
        await _commandBus.SendAsync(command);

        // Assert
        // Context should be cleared after execution
        _contextAccessor.CurrentContext.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithMultiEventCommand_ShouldReturnMultipleEvents()
    {
        // Arrange
        var command = new TestMultiEventCommand { Count = 3 };

        // Act
        var result = await _commandBus.SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task SendAsync_WithNoHandlerRegistered_ShouldThrowException()
    {
        // Arrange
        var command = new UnhandledCommand();

        // Act
        Func<Task> act = async () => await _commandBus.SendAsync(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No handler registered*");
    }

    [Fact]
    public async Task SendAsync_WithFailingHandler_ShouldReturnFailureResult()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(cqrs =>
        {
            cqrs.AddCommandHandler<FailingCommand, TestEvent, FailingCommandHandler>();
        });

        var provider = services.BuildServiceProvider();
        var commandBus = provider.GetRequiredService<ICommandBus>();

        var command = new FailingCommand();

        // Act
        var result = await commandBus.SendAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}

// Test types
public record TestEvent : DomainEvent
{
    public string Value { get; init; } = string.Empty;
}

public record TestCommand : ICommand<TestEvent>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }
    public string Value { get; init; } = string.Empty;
}

public class TestCommandHandler : ICommandHandler<TestCommand, TestEvent>
{
    public Task<CommandResult<TestEvent>> HandleAsync(
        TestCommand command,
        CancellationToken cancellationToken = default)
    {
        var @event = new TestEvent { Value = command.Value };
        var result = CommandResult<TestEvent>.SuccessResult(@event);
        return Task.FromResult(result);
    }
}

public record TestMultiEventCommand : ICommandMultiEvent
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }
    public int Count { get; init; }
}

public class TestMultiEventCommandHandler : ICommandHandlerMultiEvent<TestMultiEventCommand>
{
    public Task<CommandResult<IEnumerable<IEvent>>> HandleAsync(
        TestMultiEventCommand command,
        CancellationToken cancellationToken = default)
    {
        var events = Enumerable.Range(0, command.Count)
            .Select(i => (IEvent)new TestEvent { Value = $"Event {i}" })
            .ToList();

        var result = CommandResult<IEnumerable<IEvent>>.SuccessResult(events);
        return Task.FromResult(result);
    }
}

public record UnhandledCommand : ICommand<TestEvent>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }
}

public record FailingCommand : ICommand<TestEvent>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }
}

public class FailingCommandHandler : ICommandHandler<FailingCommand, TestEvent>
{
    public Task<CommandResult<TestEvent>> HandleAsync(
        FailingCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Command handler failed");
    }
}
