using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.CQRS.DependencyInjection;

/// <summary>
/// Builder for configuring CQRS services
/// </summary>
public class CqrsBuilder
{
    public IServiceCollection Services { get; }

    internal CqrsBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// Enable query result caching
    /// </summary>
    public CqrsBuilder WithQueryCaching()
    {
        // Cache is registered in the main extension method
        return this;
    }

    /// <summary>
    /// Enable command logging middleware
    /// </summary>
    public CqrsBuilder WithCommandLogging()
    {
        Services.AddTransient(typeof(Middleware.LoggingCommandMiddleware<>));
        return this;
    }

    /// <summary>
    /// Enable command metrics middleware
    /// </summary>
    public CqrsBuilder WithCommandMetrics()
    {
        Services.AddTransient(typeof(Middleware.MetricsCommandMiddleware<>));
        return this;
    }

    /// <summary>
    /// Enable command validation middleware
    /// </summary>
    public CqrsBuilder WithCommandValidation()
    {
        Services.AddTransient(typeof(Middleware.ValidationCommandMiddleware<>));
        return this;
    }

    /// <summary>
    /// Enable command retry middleware
    /// </summary>
    public CqrsBuilder WithCommandRetry(int maxRetries = 3, TimeSpan? retryDelay = null)
    {
        // Note: Retry middleware is registered generically per command type
        Services.AddTransient(typeof(Middleware.RetryCommandMiddleware<>));
        return this;
    }

    /// <summary>
    /// Register a custom event stream publisher
    /// </summary>
    public CqrsBuilder WithEventStreamPublisher<TPublisher>()
        where TPublisher : class, Events.IEventStreamPublisher
    {
        Services.AddSingleton<Events.IEventStreamPublisher, TPublisher>();
        return this;
    }

    /// <summary>
    /// Register a custom command middleware
    /// </summary>
    public CqrsBuilder AddCommandMiddleware<TMiddleware>()
        where TMiddleware : class, Middleware.ICommandMiddleware
    {
        Services.AddTransient<TMiddleware>();
        return this;
    }

    /// <summary>
    /// Register a custom query middleware
    /// </summary>
    public CqrsBuilder AddQueryMiddleware<TMiddleware>()
        where TMiddleware : class, Middleware.IQueryMiddleware
    {
        Services.AddTransient<TMiddleware>();
        return this;
    }
}
