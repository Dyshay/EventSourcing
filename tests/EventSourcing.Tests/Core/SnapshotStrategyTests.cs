using EventSourcing.Core.Snapshots;
using EventSourcing.Tests.TestHelpers;
using FluentAssertions;

namespace EventSourcing.Tests.Core;

public class SnapshotStrategyTests
{
    [Fact]
    public void FrequencySnapshotStrategy_ShouldCreateSnapshot_WhenEventCountReachesFrequency()
    {
        // Arrange
        var strategy = new FrequencySnapshotStrategy(10);
        var aggregate = new TestAggregate();

        // Act & Assert
        strategy.ShouldCreateSnapshot(aggregate, 9, null).Should().BeFalse();
        strategy.ShouldCreateSnapshot(aggregate, 10, null).Should().BeTrue();
        strategy.ShouldCreateSnapshot(aggregate, 15, DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void FrequencySnapshotStrategy_Constructor_ShouldThrow_WhenFrequencyIsZeroOrNegative()
    {
        // Act & Assert
        var act1 = () => new FrequencySnapshotStrategy(0);
        var act2 = () => new FrequencySnapshotStrategy(-1);

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TimeBasedSnapshotStrategy_ShouldCreateSnapshot_WhenIntervalHasElapsed()
    {
        // Arrange
        var strategy = new TimeBasedSnapshotStrategy(TimeSpan.FromHours(1));
        var aggregate = new TestAggregate();
        var oneHourAgo = DateTimeOffset.UtcNow.AddHours(-1).AddMinutes(-1);
        var thirtyMinutesAgo = DateTimeOffset.UtcNow.AddMinutes(-30);

        // Act & Assert
        strategy.ShouldCreateSnapshot(aggregate, 0, null).Should().BeFalse("no events");
        strategy.ShouldCreateSnapshot(aggregate, 5, null).Should().BeTrue("no previous snapshot exists");
        strategy.ShouldCreateSnapshot(aggregate, 5, oneHourAgo).Should().BeTrue("interval elapsed");
        strategy.ShouldCreateSnapshot(aggregate, 5, thirtyMinutesAgo).Should().BeFalse("interval not elapsed");
    }

    [Fact]
    public void TimeBasedSnapshotStrategy_Constructor_ShouldThrow_WhenIntervalIsZeroOrNegative()
    {
        // Act & Assert
        var act1 = () => new TimeBasedSnapshotStrategy(TimeSpan.Zero);
        var act2 = () => new TimeBasedSnapshotStrategy(TimeSpan.FromSeconds(-1));

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CustomSnapshotStrategy_ShouldUseCustomPredicate()
    {
        // Arrange
        var callCount = 0;
        var strategy = new CustomSnapshotStrategy((agg, count, timestamp) =>
        {
            callCount++;
            return count > 5 && count % 3 == 0;
        });
        var aggregate = new TestAggregate();

        // Act & Assert
        strategy.ShouldCreateSnapshot(aggregate, 3, null).Should().BeFalse();
        strategy.ShouldCreateSnapshot(aggregate, 6, null).Should().BeTrue();
        strategy.ShouldCreateSnapshot(aggregate, 9, DateTimeOffset.UtcNow).Should().BeTrue();
        strategy.ShouldCreateSnapshot(aggregate, 10, DateTimeOffset.UtcNow).Should().BeFalse();

        callCount.Should().Be(4);
    }

    [Fact]
    public void CustomSnapshotStrategy_Constructor_ShouldThrow_WhenPredicateIsNull()
    {
        // Act & Assert
        var act = () => new CustomSnapshotStrategy(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
