using EventSourcing.Abstractions;
using EventSourcing.Core;
using EventSourcing.CQRS.Commands;
using EventSourcing.CQRS.Context;
using EventSourcing.CQRS.Middleware;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace EventSourcing.CQRS.Tests;

public class LoggingCommandMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithSuccessfulCommand_ShouldLogAndCallNext()
    {
        // Arrange
        var middleware = new LoggingCommandMiddleware<TestCommand>(
            NullLogger<LoggingCommandMiddleware<TestCommand>>.Instance);

        var command = new TestCommand { Value = "test" };
        var context = new CommandContext(command);
        var result = new TestEvent { Value = "result" };

        CommandHandlerDelegate<TestEvent> next = () => Task.FromResult(result);

        // Act
        var actualResult = await middleware.InvokeAsync(command, context, next, CancellationToken.None);

        // Assert
        actualResult.Should().Be(result);
    }

    [Fact]
    public async Task InvokeAsync_WithFailingCommand_ShouldLogAndRethrow()
    {
        // Arrange
        var middleware = new LoggingCommandMiddleware<TestCommand>(
            NullLogger<LoggingCommandMiddleware<TestCommand>>.Instance);

        var command = new TestCommand { Value = "test" };
        var context = new CommandContext(command);

        CommandHandlerDelegate<TestEvent> next = () => throw new InvalidOperationException("Test error");

        // Act
        Func<Task> act = async () => await middleware.InvokeAsync(command, context, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test error");
    }
}

public class ValidationCommandMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithValidCommand_ShouldCallNext()
    {
        // Arrange
        var validator = Substitute.For<ICommandValidator<TestCommand>>();
        validator.ValidateAsync(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Enumerable.Empty<string>()));

        var middleware = new ValidationCommandMiddleware<TestCommand>(new[] { validator });

        var command = new TestCommand { Value = "test" };
        var context = new CommandContext(command);
        var result = new TestEvent { Value = "result" };

        CommandHandlerDelegate<TestEvent> next = () => Task.FromResult(result);

        // Act
        var actualResult = await middleware.InvokeAsync(command, context, next, CancellationToken.None);

        // Assert
        actualResult.Should().Be(result);
        await validator.Received(1).ValidateAsync(command, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidCommand_ShouldThrowValidationException()
    {
        // Arrange
        var validator = Substitute.For<ICommandValidator<TestCommand>>();
        validator.ValidateAsync(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<string>>(new[] { "Error 1", "Error 2" }));

        var middleware = new ValidationCommandMiddleware<TestCommand>(new[] { validator });

        var command = new TestCommand { Value = "test" };
        var context = new CommandContext(command);

        CommandHandlerDelegate<TestEvent> next = () => Task.FromResult(new TestEvent());

        // Act
        Func<Task> act = async () => await middleware.InvokeAsync(command, context, next, CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().HaveCount(2);
        exception.Which.Errors.Should().Contain("Error 1");
        exception.Which.Errors.Should().Contain("Error 2");
    }

    [Fact]
    public async Task InvokeAsync_WithMultipleValidators_ShouldRunAllValidators()
    {
        // Arrange
        var validator1 = Substitute.For<ICommandValidator<TestCommand>>();
        validator1.ValidateAsync(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<string>>(new[] { "Error 1" }));

        var validator2 = Substitute.For<ICommandValidator<TestCommand>>();
        validator2.ValidateAsync(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<string>>(new[] { "Error 2" }));

        var middleware = new ValidationCommandMiddleware<TestCommand>(new[] { validator1, validator2 });

        var command = new TestCommand { Value = "test" };
        var context = new CommandContext(command);

        CommandHandlerDelegate<TestEvent> next = () => Task.FromResult(new TestEvent());

        // Act
        Func<Task> act = async () => await middleware.InvokeAsync(command, context, next, CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().HaveCount(2);
        await validator1.Received(1).ValidateAsync(command, Arg.Any<CancellationToken>());
        await validator2.Received(1).ValidateAsync(command, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_WithNoValidators_ShouldCallNext()
    {
        // Arrange
        var middleware = new ValidationCommandMiddleware<TestCommand>(Enumerable.Empty<ICommandValidator<TestCommand>>());

        var command = new TestCommand { Value = "test" };
        var context = new CommandContext(command);
        var result = new TestEvent { Value = "result" };

        CommandHandlerDelegate<TestEvent> next = () => Task.FromResult(result);

        // Act
        var actualResult = await middleware.InvokeAsync(command, context, next, CancellationToken.None);

        // Assert
        actualResult.Should().Be(result);
    }
}

public class RetryCommandMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithSuccessfulCommand_ShouldNotRetry()
    {
        // Arrange
        var middleware = new RetryCommandMiddleware<TestCommand>(
            NullLogger<RetryCommandMiddleware<TestCommand>>.Instance,
            maxRetries: 3,
            retryDelay: TimeSpan.FromMilliseconds(10));

        var command = new TestCommand { Value = "test" };
        var context = new CommandContext(command);
        var result = new TestEvent { Value = "result" };

        var callCount = 0;
        CommandHandlerDelegate<TestEvent> next = () =>
        {
            callCount++;
            return Task.FromResult(result);
        };

        // Act
        var actualResult = await middleware.InvokeAsync(command, context, next, CancellationToken.None);

        // Assert
        actualResult.Should().Be(result);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_WithTransientException_ShouldRetry()
    {
        // Arrange
        var middleware = new RetryCommandMiddleware<TestCommand>(
            NullLogger<RetryCommandMiddleware<TestCommand>>.Instance,
            maxRetries: 3,
            retryDelay: TimeSpan.FromMilliseconds(10));

        var command = new TestCommand { Value = "test" };
        var context = new CommandContext(command);
        var result = new TestEvent { Value = "result" };

        var callCount = 0;
        CommandHandlerDelegate<TestEvent> next = () =>
        {
            callCount++;
            if (callCount < 2)
                throw new TimeoutException("Transient error");
            return Task.FromResult(result);
        };

        // Act
        var actualResult = await middleware.InvokeAsync(command, context, next, CancellationToken.None);

        // Assert
        actualResult.Should().Be(result);
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task InvokeAsync_WithPersistentTransientException_ShouldThrowAfterMaxRetries()
    {
        // Arrange
        var middleware = new RetryCommandMiddleware<TestCommand>(
            NullLogger<RetryCommandMiddleware<TestCommand>>.Instance,
            maxRetries: 3,
            retryDelay: TimeSpan.FromMilliseconds(10));

        var command = new TestCommand { Value = "test" };
        var context = new CommandContext(command);

        var callCount = 0;
        CommandHandlerDelegate<TestEvent> next = () =>
        {
            callCount++;
            throw new TimeoutException("Persistent transient error");
        };

        // Act
        Func<Task> act = async () => await middleware.InvokeAsync(command, context, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("Persistent transient error");
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task InvokeAsync_WithNonTransientException_ShouldNotRetry()
    {
        // Arrange
        var middleware = new RetryCommandMiddleware<TestCommand>(
            NullLogger<RetryCommandMiddleware<TestCommand>>.Instance,
            maxRetries: 3,
            retryDelay: TimeSpan.FromMilliseconds(10));

        var command = new TestCommand { Value = "test" };
        var context = new CommandContext(command);

        var callCount = 0;
        CommandHandlerDelegate<TestEvent> next = () =>
        {
            callCount++;
            throw new InvalidOperationException("Non-transient error");
        };

        // Act
        Func<Task> act = async () => await middleware.InvokeAsync(command, context, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Non-transient error");
        callCount.Should().Be(1); // Should not retry
    }

    [Fact]
    public async Task InvokeAsync_WithExponentialBackoff_ShouldIncreaseDelayBetweenRetries()
    {
        // Arrange
        var middleware = new RetryCommandMiddleware<TestCommand>(
            NullLogger<RetryCommandMiddleware<TestCommand>>.Instance,
            maxRetries: 3,
            retryDelay: TimeSpan.FromMilliseconds(50));

        var command = new TestCommand { Value = "test" };
        var context = new CommandContext(command);

        var callCount = 0;
        var callTimes = new List<DateTime>();

        CommandHandlerDelegate<TestEvent> next = () =>
        {
            callTimes.Add(DateTime.UtcNow);
            callCount++;
            if (callCount < 3)
                throw new TimeoutException("Transient error");
            return Task.FromResult(new TestEvent());
        };

        // Act
        await middleware.InvokeAsync(command, context, next, CancellationToken.None);

        // Assert
        callCount.Should().Be(3);
        callTimes.Should().HaveCount(3);

        // Verify delays increase (exponential backoff)
        var delay1 = (callTimes[1] - callTimes[0]).TotalMilliseconds;
        var delay2 = (callTimes[2] - callTimes[1]).TotalMilliseconds;

        delay1.Should().BeGreaterThanOrEqualTo(50); // First retry delay: 50ms
        delay2.Should().BeGreaterThan(delay1); // Second retry delay should be larger
    }
}
