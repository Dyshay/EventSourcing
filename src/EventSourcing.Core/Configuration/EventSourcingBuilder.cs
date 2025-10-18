using EventSourcing.Abstractions.Sagas;
using EventSourcing.Abstractions.Versioning;
using EventSourcing.Core.Projections;
using EventSourcing.Core.Publishing;
using EventSourcing.Core.Sagas;
using EventSourcing.Core.Snapshots;
using EventSourcing.Core.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Core.Configuration;

/// <summary>
/// Builder for configuring event sourcing services.
/// </summary>
public class EventSourcingBuilder
{
    public IServiceCollection Services { get; }
    public EventSourcingOptions Options { get; }

    public EventSourcingBuilder(IServiceCollection services, EventSourcingOptions options)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Configures frequency-based snapshot strategy (snapshot every N events).
    /// </summary>
    /// <param name="frequency">Number of events between snapshots</param>
    /// <returns>The builder for chaining</returns>
    public EventSourcingBuilder SnapshotEvery(int frequency)
    {
        Options.SnapshotStrategy = new FrequencySnapshotStrategy(frequency);
        return this;
    }

    /// <summary>
    /// Configures time-based snapshot strategy (snapshot every X time period).
    /// </summary>
    /// <param name="interval">Time interval between snapshots</param>
    /// <returns>The builder for chaining</returns>
    public EventSourcingBuilder SnapshotEvery(TimeSpan interval)
    {
        Options.SnapshotStrategy = new TimeBasedSnapshotStrategy(interval);
        return this;
    }

    /// <summary>
    /// Configures custom snapshot strategy.
    /// </summary>
    /// <param name="predicate">Custom predicate for determining when to snapshot</param>
    /// <returns>The builder for chaining</returns>
    public EventSourcingBuilder SnapshotWhen(Func<object, int, DateTimeOffset?, bool> predicate)
    {
        Options.SnapshotStrategy = new CustomSnapshotStrategy(predicate);
        return this;
    }

    /// <summary>
    /// Registers a projection for handling events.
    /// </summary>
    /// <typeparam name="TProjection">Type of the projection</typeparam>
    /// <returns>The builder for chaining</returns>
    public EventSourcingBuilder AddProjection<TProjection>() where TProjection : class, IProjection
    {
        Services.AddTransient<IProjection, TProjection>();
        return this;
    }

    /// <summary>
    /// Registers an external event publisher.
    /// </summary>
    /// <typeparam name="TPublisher">Type of the publisher</typeparam>
    /// <returns>The builder for chaining</returns>
    public EventSourcingBuilder AddEventPublisher<TPublisher>() where TPublisher : class, IEventPublisher
    {
        Services.AddTransient<IEventPublisher, TPublisher>();
        return this;
    }

    /// <summary>
    /// Disables event publishing (projections and external publishers won't be invoked).
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public EventSourcingBuilder DisableEventPublishing()
    {
        Options.EnableEventPublishing = false;
        return this;
    }

    /// <summary>
    /// Registers an event upcaster for transforming old event versions to new versions.
    /// </summary>
    /// <param name="upcaster">The upcaster instance to register</param>
    /// <returns>The builder for chaining</returns>
    public EventSourcingBuilder AddUpcaster(IEventUpcaster upcaster)
    {
        ArgumentNullException.ThrowIfNull(upcaster);

        // Store the upcaster to be registered later
        // We register it as a singleton factory that adds to the registry
        Services.AddSingleton(sp =>
        {
            var registry = sp.GetRequiredService<IEventUpcasterRegistry>();
            registry.RegisterUpcaster(upcaster);
            return upcaster;
        });

        return this;
    }

    /// <summary>
    /// Registers an event upcaster type for transforming old event versions to new versions.
    /// </summary>
    /// <typeparam name="TUpcaster">The upcaster type to register</typeparam>
    /// <returns>The builder for chaining</returns>
    public EventSourcingBuilder AddUpcaster<TUpcaster>() where TUpcaster : IEventUpcaster, new()
    {
        return AddUpcaster(new TUpcaster());
    }

    /// <summary>
    /// Enables event versioning with upcasting support.
    /// Call this before adding upcasters.
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public EventSourcingBuilder EnableEventVersioning()
    {
        Services.AddSingleton<IEventUpcasterRegistry, EventUpcasterRegistry>();
        return this;
    }

    /// <summary>
    /// Enables saga support with in-memory storage (suitable for development/testing).
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public EventSourcingBuilder EnableSagas()
    {
        Services.AddSingleton<ISagaStore, InMemorySagaStore>();
        Services.AddScoped<ISagaOrchestrator, SagaOrchestrator>();
        return this;
    }

    /// <summary>
    /// Enables saga support with a custom saga store implementation.
    /// </summary>
    /// <typeparam name="TSagaStore">The saga store implementation type</typeparam>
    /// <returns>The builder for chaining</returns>
    public EventSourcingBuilder EnableSagas<TSagaStore>() where TSagaStore : class, ISagaStore
    {
        Services.AddSingleton<ISagaStore, TSagaStore>();
        Services.AddScoped<ISagaOrchestrator, SagaOrchestrator>();
        return this;
    }
}
