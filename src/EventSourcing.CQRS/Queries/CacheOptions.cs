namespace EventSourcing.CQRS.Queries;

/// <summary>
/// Options for caching query results
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// The cache key to use (if null, will be auto-generated from query)
    /// </summary>
    public string? CacheKey { get; init; }

    /// <summary>
    /// How long to cache the result
    /// </summary>
    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to use sliding expiration (resets on each access)
    /// </summary>
    public bool SlidingExpiration { get; init; }

    /// <summary>
    /// Event types that should invalidate this cache entry when published
    /// </summary>
    public string[]? InvalidateOnEvents { get; init; }

    /// <summary>
    /// Creates cache options with a specific duration
    /// </summary>
    public static CacheOptions WithDuration(TimeSpan duration, bool sliding = false)
    {
        return new CacheOptions
        {
            Duration = duration,
            SlidingExpiration = sliding
        };
    }

    /// <summary>
    /// Creates cache options that invalidate on specific events
    /// </summary>
    public static CacheOptions InvalidateOn(params string[] eventTypes)
    {
        return new CacheOptions
        {
            InvalidateOnEvents = eventTypes
        };
    }

    /// <summary>
    /// Creates cache options with a specific key
    /// </summary>
    public static CacheOptions WithKey(string key, TimeSpan duration)
    {
        return new CacheOptions
        {
            CacheKey = key,
            Duration = duration
        };
    }
}
