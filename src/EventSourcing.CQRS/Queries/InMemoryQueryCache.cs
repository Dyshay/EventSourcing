using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace EventSourcing.CQRS.Queries;

/// <summary>
/// In-memory implementation of query cache using IMemoryCache for better performance
/// </summary>
public class InMemoryQueryCache : IQueryCache
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, HashSet<string>> _eventToKeysMapping = new();

    public InMemoryQueryCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<(bool found, TResult? result)> GetAsync<TResult>(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<TResult>(key, out var value))
        {
            return Task.FromResult((true, value));
        }

        return Task.FromResult((false, default(TResult)));
    }

    public Task SetAsync<TResult>(
        string key,
        TResult result,
        CacheOptions options,
        CancellationToken cancellationToken = default)
    {
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = options.Duration
        };

        // Configure sliding expiration if enabled
        if (options.SlidingExpiration)
        {
            cacheEntryOptions.SlidingExpiration = options.Duration;
            cacheEntryOptions.AbsoluteExpirationRelativeToNow = null;
        }

        _cache.Set(key, result, cacheEntryOptions);

        // Track event-to-key mappings for invalidation
        if (options.InvalidateOnEvents != null)
        {
            foreach (var eventType in options.InvalidateOnEvents)
            {
                _eventToKeysMapping.AddOrUpdate(
                    eventType,
                    _ => new HashSet<string> { key },
                    (_, set) =>
                    {
                        set.Add(key);
                        return set;
                    });
            }
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task InvalidateByEventAsync(string eventType, CancellationToken cancellationToken = default)
    {
        if (_eventToKeysMapping.TryGetValue(eventType, out var keys))
        {
            foreach (var key in keys)
            {
                _cache.Remove(key);
            }

            _eventToKeysMapping.TryRemove(eventType, out _);
        }

        return Task.CompletedTask;
    }
}
