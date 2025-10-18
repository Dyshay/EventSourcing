using EventSourcing.CQRS.Queries;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace EventSourcing.CQRS.Tests;

public class InMemoryQueryCacheTests
{
    [Fact]
    public async Task SetAsync_ThenGetAsync_ShouldReturnCachedValue()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new InMemoryQueryCache(memoryCache);
        var key = "test-key";
        var value = "test-value";
        var options = CacheOptions.WithDuration(TimeSpan.FromMinutes(5));

        // Act
        await cache.SetAsync(key, value, options);
        var (found, result) = await cache.GetAsync<string>(key);

        // Assert
        found.Should().BeTrue();
        result.Should().Be(value);
    }

    [Fact]
    public async Task GetAsync_WithExpiredEntry_ShouldReturnNotFound()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new InMemoryQueryCache(memoryCache);
        var key = "test-key";
        var value = "test-value";
        var options = CacheOptions.WithDuration(TimeSpan.FromMilliseconds(100));

        // Act
        await cache.SetAsync(key, value, options);
        await Task.Delay(200); // Wait for expiration
        var (found, _) = await cache.GetAsync<string>(key);

        // Assert
        found.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveCachedValue()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new InMemoryQueryCache(memoryCache);
        var key = "test-key";
        var value = "test-value";
        var options = CacheOptions.WithDuration(TimeSpan.FromMinutes(5));

        await cache.SetAsync(key, value, options);

        // Act
        await cache.RemoveAsync(key);
        var (found, _) = await cache.GetAsync<string>(key);

        // Assert
        found.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidateByEventAsync_ShouldRemoveRelatedCaches()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new InMemoryQueryCache(memoryCache);
        var key1 = "test-key-1";
        var key2 = "test-key-2";
        var value = "test-value";
        var options = new CacheOptions
        {
            Duration = TimeSpan.FromMinutes(5),
            InvalidateOnEvents = new[] { "TestEvent", "OtherEvent" }
        };

        await cache.SetAsync(key1, value, options);
        await cache.SetAsync(key2, value, options);

        // Act
        await cache.InvalidateByEventAsync("TestEvent");

        // Assert
        var (found1, _) = await cache.GetAsync<string>(key1);
        var (found2, _) = await cache.GetAsync<string>(key2);
        found1.Should().BeFalse();
        found2.Should().BeFalse();
    }

    [Fact]
    public async Task SlidingExpiration_ShouldExtendCacheLifetime()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new InMemoryQueryCache(memoryCache);
        var key = "test-key";
        var value = "test-value";
        var options = new CacheOptions
        {
            Duration = TimeSpan.FromMilliseconds(500),
            SlidingExpiration = true
        };

        await cache.SetAsync(key, value, options);

        // Act
        await Task.Delay(300);
        var (found1, _) = await cache.GetAsync<string>(key); // Should extend expiration
        await Task.Delay(300);
        var (found2, _) = await cache.GetAsync<string>(key);

        // Assert
        found1.Should().BeTrue();
        found2.Should().BeTrue(); // Still valid because of sliding expiration
    }
}
