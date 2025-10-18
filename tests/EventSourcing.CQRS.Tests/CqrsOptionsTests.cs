using EventSourcing.Abstractions;
using EventSourcing.Core;
using EventSourcing.CQRS.Commands;
using EventSourcing.CQRS.Configuration;
using EventSourcing.CQRS.Context;
using EventSourcing.CQRS.DependencyInjection;
using EventSourcing.CQRS.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;

namespace EventSourcing.CQRS.Tests;

public class CqrsOptionsTests : IDisposable
{
    private ServiceProvider? _serviceProvider;

    [Fact]
    public void DefaultOptions_Should_EnableAllFeatures()
    {
        // Arrange & Act
        var options = CqrsOptions.Default();

        // Assert
        Assert.True(options.EnableAuditTrail);
        Assert.True(options.EnableLogging);
        Assert.True(options.EnableQueryCache);
    }

    [Fact]
    public void HighPerformanceOptions_Should_DisableAuditTrailAndLogging()
    {
        // Arrange & Act
        var options = CqrsOptions.HighPerformance();

        // Assert
        Assert.False(options.EnableAuditTrail);
        Assert.False(options.EnableLogging);
        Assert.True(options.EnableQueryCache);
    }

    [Fact]
    public void CustomOptions_Should_RespectProvidedValues()
    {
        // Arrange & Act
        var options = CqrsOptions.Custom(
            enableAuditTrail: false,
            enableLogging: true,
            enableQueryCache: false
        );

        // Assert
        Assert.False(options.EnableAuditTrail);
        Assert.True(options.EnableLogging);
        Assert.False(options.EnableQueryCache);
    }

    [Fact]
    public async Task CommandBus_WithAuditTrailDisabled_Should_NotCreateCommandContext()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(
            cqrs => cqrs.AddCommandHandler<TestCommand, TestEvent, TestCommandHandler>(),
            CqrsOptions.Custom(enableAuditTrail: false)
        );

        _serviceProvider = services.BuildServiceProvider();
        var commandBus = _serviceProvider.GetRequiredService<ICommandBus>();
        var contextAccessor = _serviceProvider.GetRequiredService<ICommandContextAccessor>();

        var command = new TestCommand();

        // Act
        await commandBus.SendAsync(command);

        // Assert
        Assert.Null(contextAccessor.CurrentContext);
    }

    [Fact]
    public async Task CommandBus_WithAuditTrailEnabled_Should_CreateCommandContext()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(
            cqrs => cqrs.AddCommandHandler<TestCommand, TestEvent, TestCommandHandler>(),
            CqrsOptions.Default()
        );

        _serviceProvider = services.BuildServiceProvider();
        var commandBus = _serviceProvider.GetRequiredService<ICommandBus>();
        var contextAccessor = _serviceProvider.GetRequiredService<ICommandContextAccessor>();

        var command = new TestCommand();

        // Act
        var result = await commandBus.SendAsync(command);

        // Assert - Context should be cleared after command execution
        Assert.Null(contextAccessor.CurrentContext);
        Assert.True(result.Success);
    }

    [Fact]
    public void CommandContextPool_Should_BeRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(cqrs => cqrs.AddCommandHandler<TestCommand, TestEvent, TestCommandHandler>());

        _serviceProvider = services.BuildServiceProvider();

        // Act
        var pool = _serviceProvider.GetService<ObjectPool<CommandContext>>();

        // Assert
        Assert.NotNull(pool);
    }

    [Fact]
    public void CommandContextPool_Should_ReuseInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(cqrs => cqrs.AddCommandHandler<TestCommand, TestEvent, TestCommandHandler>());

        _serviceProvider = services.BuildServiceProvider();
        var pool = _serviceProvider.GetRequiredService<ObjectPool<CommandContext>>();

        // Act
        var context1 = pool.Get();
        var id1 = context1.GetHashCode();
        pool.Return(context1);

        var context2 = pool.Get();
        var id2 = context2.GetHashCode();
        pool.Return(context2);

        // Assert - Should reuse the same instance
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void CommandContext_Reset_Should_ClearAllProperties()
    {
        // Arrange
        var context = new CommandContext(new TestCommand())
        {
            AggregateId = Guid.NewGuid(),
            AggregateVersion = 5,
            ErrorMessage = "Test error"
        };
        context.RecordEvent(new TestEvent());

        // Act
        context.Reset();

        // Assert
        Assert.Equal(Guid.Empty, context.CommandId);
        Assert.Equal(string.Empty, context.CommandType);
        Assert.Null(context.AggregateId);
        Assert.Null(context.AggregateVersion);
        Assert.Null(context.ErrorMessage);
        Assert.Empty(context.GeneratedEvents);
    }

    [Fact]
    public void CommandContext_Initialize_Should_SetProperties()
    {
        // Arrange
        var context = new CommandContext();
        var command = new TestCommand();

        // Act
        context.Initialize(command);

        // Assert
        Assert.Equal(command.CommandId, context.CommandId);
        Assert.Equal(nameof(TestCommand), context.CommandType);
        Assert.NotNull(context.CorrelationId);
        Assert.True(context.Success);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    // Test types
    private record TestEvent : DomainEvent
    {
        public string Value { get; init; } = "test";
    }

    private record TestCommand : ICommand<TestEvent>
    {
        public Guid CommandId { get; init; } = Guid.NewGuid();
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
        public Dictionary<string, object>? Metadata { get; init; }
    }

    private class TestCommandHandler : ICommandHandler<TestCommand, TestEvent>
    {
        public Task<CommandResult<TestEvent>> HandleAsync(
            TestCommand command,
            CancellationToken cancellationToken = default)
        {
            var @event = new TestEvent();
            return Task.FromResult(CommandResult<TestEvent>.SuccessResult(@event));
        }
    }
}
