using EventSourcing.CQRS.Commands;
using EventSourcing.CQRS.Configuration;
using EventSourcing.CQRS.Context;
using EventSourcing.CQRS.Events;
using EventSourcing.CQRS.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;
using System.Reflection;

namespace EventSourcing.CQRS.DependencyInjection;

/// <summary>
/// Extension methods for configuring CQRS services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds CQRS services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Optional configuration action for the CQRS builder</param>
    /// <param name="options">Optional CQRS options. If null, default options will be used.</param>
    public static CqrsBuilder AddCqrs(
        this IServiceCollection services,
        Action<CqrsBuilder>? configure = null,
        CqrsOptions? options = null)
    {
        // Register CQRS options
        services.Configure<CqrsOptions>(opts =>
        {
            if (options != null)
            {
                opts.EnableAuditTrail = options.EnableAuditTrail;
                opts.EnableLogging = options.EnableLogging;
                opts.EnableQueryCache = options.EnableQueryCache;
            }
        });

        // Register CommandContext object pool for better performance
        services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.TryAddSingleton(serviceProvider =>
        {
            var provider = serviceProvider.GetRequiredService<ObjectPoolProvider>();
            var policy = new CommandContextPoolPolicy();
            return provider.Create(policy);
        });

        // Register core services
        services.TryAddSingleton<ICommandBus, CommandBus>();
        services.TryAddSingleton<IQueryBus, QueryBus>();
        services.TryAddSingleton<IEventBus, EventBus>();
        services.TryAddSingleton<ICommandContextAccessor, CommandContextAccessor>();

        // Register memory cache for query caching
        services.AddMemoryCache();

        // Register query cache (in-memory by default)
        services.TryAddSingleton<IQueryCache, InMemoryQueryCache>();

        var builder = new CqrsBuilder(services);
        configure?.Invoke(builder);

        return builder;
    }

    /// <summary>
    /// Registers all command handlers from the specified assembly
    /// </summary>
    public static CqrsBuilder AddCommandHandlers(
        this CqrsBuilder builder,
        Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType &&
                    (i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
                     i.GetGenericTypeDefinition() == typeof(ICommandHandlerMultiEvent<>)))
                .Select(i => new { Service = i, Implementation = t }));

        foreach (var handler in handlerTypes)
        {
            builder.Services.AddTransient(handler.Service, handler.Implementation);
        }

        return builder;
    }

    /// <summary>
    /// Registers all query handlers from the specified assembly
    /// </summary>
    public static CqrsBuilder AddQueryHandlers(
        this CqrsBuilder builder,
        Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))
                .Select(i => new { Service = i, Implementation = t }));

        foreach (var handler in handlerTypes)
        {
            builder.Services.AddTransient(handler.Service, handler.Implementation);
        }

        return builder;
    }

    /// <summary>
    /// Registers all event handlers from the specified assembly
    /// </summary>
    public static CqrsBuilder AddEventHandlers(
        this CqrsBuilder builder,
        Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                .Select(i => new { Service = i, Implementation = t }));

        foreach (var handler in handlerTypes)
        {
            builder.Services.AddTransient(handler.Service, handler.Implementation);
        }

        return builder;
    }

    /// <summary>
    /// Registers all handlers (commands, queries, events) from the specified assembly
    /// </summary>
    public static CqrsBuilder AddHandlersFromAssembly(
        this CqrsBuilder builder,
        Assembly assembly)
    {
        return builder
            .AddCommandHandlers(assembly)
            .AddQueryHandlers(assembly)
            .AddEventHandlers(assembly);
    }

    /// <summary>
    /// Registers a specific command handler
    /// </summary>
    public static CqrsBuilder AddCommandHandler<TCommand, TEvent, THandler>(
        this CqrsBuilder builder)
        where TCommand : ICommand<TEvent>
        where TEvent : EventSourcing.Abstractions.IEvent
        where THandler : class, ICommandHandler<TCommand, TEvent>
    {
        builder.Services.AddTransient<ICommandHandler<TCommand, TEvent>, THandler>();
        return builder;
    }

    /// <summary>
    /// Registers a specific query handler
    /// </summary>
    public static CqrsBuilder AddQueryHandler<TQuery, TResult, THandler>(
        this CqrsBuilder builder)
        where TQuery : IQuery<TResult>
        where THandler : class, IQueryHandler<TQuery, TResult>
    {
        builder.Services.AddTransient<IQueryHandler<TQuery, TResult>, THandler>();
        return builder;
    }

    /// <summary>
    /// Registers a specific event handler
    /// </summary>
    public static CqrsBuilder AddEventHandler<TEvent, THandler>(
        this CqrsBuilder builder)
        where TEvent : EventSourcing.Abstractions.IEvent
        where THandler : class, IEventHandler<TEvent>
    {
        builder.Services.AddTransient<IEventHandler<TEvent>, THandler>();
        return builder;
    }

    /// <summary>
    /// Registers a command validator
    /// </summary>
    public static CqrsBuilder AddCommandValidator<TCommand, TValidator>(
        this CqrsBuilder builder)
        where TCommand : ICommand
        where TValidator : class, Middleware.ICommandValidator<TCommand>
    {
        builder.Services.AddTransient<Middleware.ICommandValidator<TCommand>, TValidator>();
        return builder;
    }
}
