using EventSourcing.Abstractions;
using EventSourcing.Core;
using EventSourcing.Core.Configuration;
using EventSourcing.Core.Publishing;
using EventSourcing.Core.Snapshots;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace EventSourcing.MongoDB;

/// <summary>
/// Extension methods for configuring event sourcing services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds event sourcing services to the service collection.
    /// You must call a storage provider extension (UseMongoDB, UsePostgreSQL, etc.) inside the configure action.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddEventSourcing(
        this IServiceCollection services,
        Action<EventSourcingBuilder> configure)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        var options = new EventSourcingOptions();
        var builder = new EventSourcingBuilder(services, options);

        // Apply user configuration (must call UseMongoDB or other provider)
        configure(builder);

        // Validate that a storage provider was registered
        var providerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventSourcingStorageProvider));
        if (providerDescriptor == null)
        {
            throw new InvalidOperationException(
                "No storage provider configured. Call UseMongoDB(), UsePostgreSQL(), or another provider extension in the configure action.");
        }

        // Register event store and snapshot store from the provider
        services.AddSingleton(sp =>
        {
            var provider = sp.GetRequiredService<IEventSourcingStorageProvider>();
            provider.ValidateConfiguration();
            return provider.CreateEventStore();
        });

        services.AddSingleton(sp =>
        {
            var provider = sp.GetRequiredService<IEventSourcingStorageProvider>();
            return provider.CreateSnapshotStore();
        });

        // Register snapshot strategy (default to frequency of 10 if not configured)
        var snapshotStrategy = options.SnapshotStrategy ?? new FrequencySnapshotStrategy(10);
        services.AddSingleton<ISnapshotStrategy>(snapshotStrategy);

        // Register event bus if publishing is enabled
        if (options.EnableEventPublishing)
        {
            services.AddSingleton<IEventBus, EventBus>();
        }

        // Register repository (generic, will be resolved for each aggregate type)
        services.AddScoped(typeof(IAggregateRepository<,>), typeof(AggregateRepository<,>));

        return services;
    }

    /// <summary>
    /// Registers an aggregate repository for a specific aggregate type.
    /// This is optional - repositories can be resolved directly via IAggregateRepository&lt;TAggregate, TId&gt;.
    /// </summary>
    /// <typeparam name="TAggregate">Type of the aggregate</typeparam>
    /// <typeparam name="TId">Type of the aggregate identifier</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAggregateRepository<TAggregate, TId>(this IServiceCollection services)
        where TAggregate : IAggregate<TId>, new()
        where TId : notnull
    {
        services.AddScoped<IAggregateRepository<TAggregate, TId>, AggregateRepository<TAggregate, TId>>();
        return services;
    }
}
