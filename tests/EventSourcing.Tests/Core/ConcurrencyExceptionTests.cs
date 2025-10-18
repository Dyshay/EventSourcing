using EventSourcing.Abstractions;
using FluentAssertions;

namespace EventSourcing.Tests.Core;

public class ConcurrencyExceptionTests
{
    [Fact]
    public void Constructor_WithAggregateIdAndVersions_ShouldSetProperties()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var expectedVersion = 5;
        var actualVersion = 7;

        // Act
        var exception = new ConcurrencyException(aggregateId, expectedVersion, actualVersion);

        // Assert
        exception.Message.Should().Contain(aggregateId.ToString());
        exception.Message.Should().Contain("5");
        exception.Message.Should().Contain("7");
    }

    [Fact]
    public void Constructor_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Custom concurrency error message";

        // Act
        var exception = new ConcurrencyException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_ShouldSetBoth()
    {
        // Arrange
        var message = "Custom concurrency error";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new ConcurrencyException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void ConcurrencyException_ShouldBeException()
    {
        // Arrange & Act
        var exception = new ConcurrencyException("Test");

        // Assert
        exception.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void Constructor_WithZeroVersions_ShouldHandleCorrectly()
    {
        // Arrange & Act
        var exception = new ConcurrencyException(Guid.NewGuid(), 0, 1);

        // Assert
        exception.Message.Should().Contain("0");
        exception.Message.Should().Contain("1");
    }

    [Fact]
    public void Constructor_WithLargeVersionNumbers_ShouldHandleCorrectly()
    {
        // Arrange & Act
        var exception = new ConcurrencyException(Guid.NewGuid(), 1000000, 1000005);

        // Assert
        exception.Message.Should().Contain("1000000");
        exception.Message.Should().Contain("1000005");
    }
}
