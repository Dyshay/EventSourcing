using EventSourcing.Abstractions;
using EventSourcing.Abstractions.Sagas;
using EventSourcing.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace EventSourcing.MongoDB;

/// <summary>
/// Extension methods for configuring MongoDB as the event sourcing storage provider.
/// </summary>
public static class MongoDBExtensions
{
    /// <summary>
    /// Configures MongoDB as the storage provider for event sourcing.
    /// </summary>
    /// <param name="builder">The event sourcing builder</param>
    /// <param name="connectionString">MongoDB connection string</param>
    /// <param name="databaseName">MongoDB database name</param>
    /// <returns>The builder for chaining</returns>
    public static EventSourcingBuilder UseMongoDB(
        this EventSourcingBuilder builder,
        string connectionString,
        string databaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty", nameof(databaseName));

        // Create and register the MongoDB storage provider
        var provider = new MongoDBStorageProvider(connectionString, databaseName);
        builder.Services.AddSingleton<IEventSourcingStorageProvider>(provider);

        // Store configuration for later use
        builder.Options.ConnectionString = connectionString;
        builder.Options.DatabaseName = databaseName;

        return builder;
    }

    /// <summary>
    /// Initializes MongoDB storage (creates indexes).
    /// Call this during application startup after all aggregates are registered.
    /// </summary>
    /// <param name="builder">The event sourcing builder</param>
    /// <param name="aggregateTypes">Types of aggregates to initialize storage for</param>
    /// <returns>The builder for chaining</returns>
    public static EventSourcingBuilder InitializeMongoDB(
        this EventSourcingBuilder builder,
        params string[] aggregateTypes)
    {
        builder.Services.AddHostedService(sp =>
        {
            var provider = sp.GetRequiredService<IEventSourcingStorageProvider>();
            return new MongoDBInitializationService(provider, aggregateTypes);
        });

        return builder;
    }

    /// <summary>
    /// Registers event types for serialization/deserialization.
    /// Scans the specified assembly for types implementing IEvent and registers them.
    /// </summary>
    /// <param name="builder">The event sourcing builder</param>
    /// <param name="assembly">Assembly to scan for event types</param>
    /// <returns>The builder for chaining</returns>
    public static EventSourcingBuilder RegisterEventsFromAssembly(
        this EventSourcingBuilder builder,
        System.Reflection.Assembly assembly)
    {
        var eventTypes = assembly.GetTypes()
            .Where(t => typeof(Abstractions.IEvent).IsAssignableFrom(t)
                && !t.IsAbstract
                && !t.IsInterface);

        foreach (var eventType in eventTypes)
        {
            Serialization.EventSerializer.RegisterEventType(eventType);
        }

        return builder;
    }

    /// <summary>
    /// Registers specific event types for serialization/deserialization.
    /// </summary>
    /// <param name="builder">The event sourcing builder</param>
    /// <param name="eventTypes">Event types to register</param>
    /// <returns>The builder for chaining</returns>
    public static EventSourcingBuilder RegisterEventTypes(
        this EventSourcingBuilder builder,
        params Type[] eventTypes)
    {
        foreach (var eventType in eventTypes)
        {
            Serialization.EventSerializer.RegisterEventType(eventType);
        }

        return builder;
    }

    /// <summary>
    /// Enables saga support with MongoDB storage.
    /// Requires UseMongoDB to be called first.
    /// </summary>
    /// <param name="builder">The event sourcing builder</param>
    /// <returns>The builder for chaining</returns>
    public static EventSourcingBuilder EnableMongoDBSagas(this EventSourcingBuilder builder)
    {
        // Get the MongoDB database from the storage provider
        builder.Services.AddSingleton<ISagaStore>(sp =>
        {
            var provider = sp.GetRequiredService<IEventSourcingStorageProvider>();
            if (provider is not MongoDBStorageProvider mongoProvider)
            {
                throw new InvalidOperationException(
                    "EnableMongoDBSagas requires UseMongoDB to be called first");
            }

            var database = mongoProvider.GetDatabase();
            return new MongoSagaStore(database);
        });

        builder.Services.AddScoped<ISagaOrchestrator, Core.Sagas.SagaOrchestrator>();
        return builder;
    }
}
