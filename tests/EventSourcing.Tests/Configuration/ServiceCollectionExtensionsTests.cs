using EventSourcing.Abstractions;
using EventSourcing.Core;
using EventSourcing.Core.Publishing;
using EventSourcing.Core.Snapshots;
using EventSourcing.MongoDB;
using EventSourcing.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Moq;

namespace EventSourcing.Tests.Configuration;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEventSourcing_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddEventSourcing(builder => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddEventSourcing_WithNullConfigure_ShouldThrowArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddEventSourcing(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddEventSourcing_WithoutStorageProvider_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddEventSourcing(builder =>
        {
            // Not calling UseMongoDB or any other provider
        });

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No storage provider configured*");
    }

    [Fact]
    public void AddEventSourcing_WithMongoDB_ShouldRegisterAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockDatabase = new Mock<IMongoDatabase>();

        // Act
        services.AddEventSourcing(builder =>
        {
            var provider = new MongoDBStorageProvider(mockDatabase.Object);
            builder.Services.AddSingleton<IEventSourcingStorageProvider>(provider);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IEventSourcingStorageProvider>().Should().NotBeNull();
        serviceProvider.GetService<IEventStore>().Should().NotBeNull();
        serviceProvider.GetService<ISnapshotStore>().Should().NotBeNull();
        serviceProvider.GetService<ISnapshotStrategy>().Should().NotBeNull();
        serviceProvider.GetService<IAggregateRepository<TestAggregate, Guid>>().Should().NotBeNull();
    }

    [Fact]
    public void AddEventSourcing_DefaultSnapshotStrategy_ShouldBeFrequency10()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockDatabase = new Mock<IMongoDatabase>();

        // Act
        services.AddEventSourcing(builder =>
        {
            var provider = new MongoDBStorageProvider(mockDatabase.Object);
            builder.Services.AddSingleton<IEventSourcingStorageProvider>(provider);
        });

        var serviceProvider = services.BuildServiceProvider();
        var strategy = serviceProvider.GetService<ISnapshotStrategy>();

        // Assert
        strategy.Should().NotBeNull();
        strategy.Should().BeOfType<FrequencySnapshotStrategy>();

        var frequencyStrategy = (FrequencySnapshotStrategy)strategy!;
        // Test default frequency by checking behavior
        var testAggregate = new TestAggregate();
        frequencyStrategy.ShouldCreateSnapshot(testAggregate, 10, null).Should().BeTrue();
        frequencyStrategy.ShouldCreateSnapshot(testAggregate, 9, null).Should().BeFalse();
    }

    [Fact]
    public void AddEventSourcing_WithCustomSnapshotStrategy_ShouldUseCustomStrategy()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockDatabase = new Mock<IMongoDatabase>();

        // Act
        services.AddEventSourcing(builder =>
        {
            var provider = new MongoDBStorageProvider(mockDatabase.Object);
            builder.Services.AddSingleton<IEventSourcingStorageProvider>(provider);
            builder.SnapshotEvery(5);
        });

        var serviceProvider = services.BuildServiceProvider();
        var strategy = serviceProvider.GetService<ISnapshotStrategy>();

        // Assert
        strategy.Should().NotBeNull();
        strategy.Should().BeOfType<FrequencySnapshotStrategy>();

        var frequencyStrategy = (FrequencySnapshotStrategy)strategy!;
        var testAggregate = new TestAggregate();
        frequencyStrategy.ShouldCreateSnapshot(testAggregate, 5, null).Should().BeTrue();
        frequencyStrategy.ShouldCreateSnapshot(testAggregate, 4, null).Should().BeFalse();
    }

    [Fact]
    public void AddEventSourcing_WithEventPublishingEnabled_ShouldRegisterEventBus()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockDatabase = new Mock<IMongoDatabase>();

        // Act
        services.AddEventSourcing(builder =>
        {
            var provider = new MongoDBStorageProvider(mockDatabase.Object);
            builder.Services.AddSingleton<IEventSourcingStorageProvider>(provider);
            // Publishing is enabled by default
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IEventBus>().Should().NotBeNull();
    }

    [Fact]
    public void AddEventSourcing_WithEventPublishingDisabled_ShouldNotRegisterEventBus()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockDatabase = new Mock<IMongoDatabase>();

        // Act
        services.AddEventSourcing(builder =>
        {
            var provider = new MongoDBStorageProvider(mockDatabase.Object);
            builder.Services.AddSingleton<IEventSourcingStorageProvider>(provider);
            builder.DisableEventPublishing();
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IEventBus>().Should().BeNull();
    }

    [Fact]
    public void AddAggregateRepository_ShouldRegisterRepositoryForSpecificAggregate()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockDatabase = new Mock<IMongoDatabase>();

        services.AddEventSourcing(builder =>
        {
            var provider = new MongoDBStorageProvider(mockDatabase.Object);
            builder.Services.AddSingleton<IEventSourcingStorageProvider>(provider);
        });

        // Act
        services.AddAggregateRepository<TestAggregate, Guid>();

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IAggregateRepository<TestAggregate, Guid>>().Should().NotBeNull();
    }
}
