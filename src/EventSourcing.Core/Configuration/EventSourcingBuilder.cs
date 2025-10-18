using EventSourcing.Core.Projections;
using EventSourcing.Core.Publishing;
using EventSourcing.Core.Snapshots;
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
}
