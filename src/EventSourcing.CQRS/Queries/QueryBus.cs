using System.Collections.Concurrent;
using System.Text.Json;
using EventSourcing.CQRS.Configuration;
using EventSourcing.CQRS.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventSourcing.CQRS.Queries;

/// <summary>
/// Default implementation of query bus with caching support
/// </summary>
public partial class QueryBus : IQueryBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IQueryCache? _cache;
    private readonly ILogger<QueryBus> _logger;
    private readonly CqrsOptions _options;

    // Cache pour éviter la création répétée de types génériques
    private readonly ConcurrentDictionary<(Type QueryType, Type ResultType), Type> _handlerTypeCache = new();

    public QueryBus(
        IServiceProvider serviceProvider,
        ILogger<QueryBus> logger,
        IOptions<CqrsOptions> options,
        IQueryCache? cache = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _cache = cache;
    }

    public Task<TResult> SendAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(query, null, cancellationToken);
    }

    public async Task<TResult> SendAsync<TResult>(
        IQuery<TResult> query,
        CacheOptions? cacheOptions,
        CancellationToken cancellationToken = default)
    {
        var queryType = query.GetType();

        // Utiliser le cache pour éviter MakeGenericType à chaque appel
        var handlerType = _handlerTypeCache.GetOrAdd(
            (queryType, typeof(TResult)),
            key => typeof(IQueryHandler<,>).MakeGenericType(key.QueryType, key.ResultType));
        var handler = _serviceProvider.GetService(handlerType);

        if (handler == null)
        {
            throw new InvalidOperationException(
                $"No handler registered for query type {queryType.Name}");
        }

        // Try cache first if caching is enabled
        if (_options.EnableQueryCache && cacheOptions != null && _cache != null)
        {
            var cacheKey = cacheOptions.CacheKey ?? GenerateCacheKey(query);
            var (found, cachedResult) = await _cache.GetAsync<TResult>(cacheKey, cancellationToken);

            if (found)
            {
                if (_options.EnableLogging)
                {
                    LogCacheHit(queryType.Name, cacheKey);
                }

                return cachedResult!;
            }

            if (_options.EnableLogging)
            {
                LogCacheMiss(queryType.Name, cacheKey);
            }
        }

        try
        {
            if (_options.EnableLogging)
            {
                LogQueryExecution(queryType.Name, query.QueryId);
            }

            var startTime = _options.EnableLogging ? DateTimeOffset.UtcNow : default;

            // Execute with middleware pipeline
            var result = await ExecuteWithMiddleware(query, handler, cancellationToken);

            if (_options.EnableLogging)
            {
                var executionTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
                LogQuerySuccess(queryType.Name, executionTime);
            }

            // Cache the result if caching is enabled
            if (_options.EnableQueryCache && cacheOptions != null && _cache != null && result != null)
            {
                var cacheKey = cacheOptions.CacheKey ?? GenerateCacheKey(query);
                await _cache.SetAsync(cacheKey, result, cacheOptions, cancellationToken);

                if (_options.EnableLogging)
                {
                    LogCacheSet(queryType.Name, cacheKey);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            if (_options.EnableLogging)
            {
                LogQueryError(ex, queryType.Name, ex.Message);
            }

            throw;
        }
    }

    private async Task<TResult> ExecuteWithMiddleware<TResult>(
        IQuery<TResult> query,
        object handler,
        CancellationToken cancellationToken)
    {
        var queryType = query.GetType();
        var middlewareType = typeof(IQueryMiddleware<,>).MakeGenericType(queryType, typeof(TResult));
        var middleware = _serviceProvider.GetServices(middlewareType)
            .Cast<IQueryMiddleware>()
            .OrderBy(m => m.Order)
            .ToList();

        // Build the pipeline
        QueryHandlerDelegate<TResult> pipeline = async () =>
        {
            var handleMethod = handler.GetType().GetMethod("HandleAsync");
            if (handleMethod == null)
            {
                throw new InvalidOperationException("Handler does not have HandleAsync method");
            }

            var task = (Task<TResult>)handleMethod.Invoke(
                handler,
                new object[] { query, cancellationToken })!;

            return await task;
        };

        // Wrap with middleware in reverse order
        foreach (var mw in middleware.AsEnumerable().Reverse())
        {
            var currentPipeline = pipeline;
            var currentMiddleware = mw;

            pipeline = async () =>
            {
                var invokeMethod = currentMiddleware.GetType()
                    .GetMethod("InvokeAsync");

                if (invokeMethod == null)
                {
                    throw new InvalidOperationException(
                        $"Middleware {currentMiddleware.GetType().Name} does not have InvokeAsync method");
                }

                var task = (Task<TResult>)invokeMethod.Invoke(
                    currentMiddleware,
                    new object[] { query, currentPipeline, cancellationToken })!;

                return await task;
            };
        }

        return await pipeline();
    }

    private static string GenerateCacheKey<TResult>(IQuery<TResult> query)
    {
        // Generate cache key from query type and properties
        var queryJson = JsonSerializer.Serialize(query);
        return $"{query.GetType().Name}:{queryJson.GetHashCode():X}";
    }

    // LoggerMessage source generators for better performance
    [LoggerMessage(EventId = 10, Level = LogLevel.Debug,
        Message = "Cache hit for query {QueryType} with key {CacheKey}")]
    private partial void LogCacheHit(string queryType, string cacheKey);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug,
        Message = "Cache miss for query {QueryType} with key {CacheKey}")]
    private partial void LogCacheMiss(string queryType, string cacheKey);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information,
        Message = "Executing query {QueryType} with ID {QueryId}")]
    private partial void LogQueryExecution(string queryType, Guid queryId);

    [LoggerMessage(EventId = 13, Level = LogLevel.Information,
        Message = "Query {QueryType} executed successfully in {ExecutionTime}ms")]
    private partial void LogQuerySuccess(string queryType, double executionTime);

    [LoggerMessage(EventId = 14, Level = LogLevel.Debug,
        Message = "Cached result for query {QueryType} with key {CacheKey}")]
    private partial void LogCacheSet(string queryType, string cacheKey);

    [LoggerMessage(EventId = 15, Level = LogLevel.Error,
        Message = "Query {QueryType} failed: {ErrorMessage}")]
    private partial void LogQueryError(Exception ex, string queryType, string errorMessage);
}
