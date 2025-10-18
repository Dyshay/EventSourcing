using EventSourcing.CQRS.DependencyInjection;
using EventSourcing.CQRS.Queries;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventSourcing.CQRS.Tests;

public class QueryBusTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IQueryBus _queryBus;
    private readonly IQueryCache _cache;

    public QueryBusTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(cqrs =>
        {
            cqrs.AddQueryHandler<TestQuery, string, TestQueryHandler>();
        });

        _serviceProvider = services.BuildServiceProvider();
        _queryBus = _serviceProvider.GetRequiredService<IQueryBus>();
        _cache = _serviceProvider.GetRequiredService<IQueryCache>();
    }

    [Fact]
    public async Task SendAsync_WithValidQuery_ShouldReturnResult()
    {
        // Arrange
        var query = new TestQuery { Id = 1 };

        // Act
        var result = await _queryBus.SendAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be("Result for 1");
    }

    [Fact]
    public async Task SendAsync_WithCacheOptions_ShouldCacheResult()
    {
        // Arrange
        var query = new TestQuery { Id = 1 };
        var cacheOptions = CacheOptions.WithDuration(TimeSpan.FromMinutes(5));

        // Act
        var result1 = await _queryBus.SendAsync(query, cacheOptions);
        var result2 = await _queryBus.SendAsync(query, cacheOptions);

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public async Task SendAsync_WithExpiredCache_ShouldRefetchResult()
    {
        // Arrange
        var query = new TestQuery { Id = 1 };
        var cacheOptions = CacheOptions.WithDuration(TimeSpan.FromMilliseconds(100));

        // Act
        var result1 = await _queryBus.SendAsync(query, cacheOptions);
        await Task.Delay(200); // Wait for cache to expire
        var result2 = await _queryBus.SendAsync(query, cacheOptions);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAsync_WithNoHandlerRegistered_ShouldThrowException()
    {
        // Arrange
        var query = new UnhandledQuery();

        // Act
        Func<Task> act = async () => await _queryBus.SendAsync(query);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No handler registered*");
    }

    [Fact]
    public async Task SendAsync_WithSlidingExpiration_ShouldExtendCache()
    {
        // Arrange
        var counter = new CallCounter();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(cqrs =>
        {
            cqrs.AddQueryHandler<TestQuery, string, TestCountingQueryHandler>();
        });
        services.AddSingleton(counter);

        var serviceProvider = services.BuildServiceProvider();
        var queryBus = serviceProvider.GetRequiredService<IQueryBus>();

        var query = new TestQuery { Id = 1 };
        var cacheOptions = new CacheOptions
        {
            Duration = TimeSpan.FromMilliseconds(500),
            SlidingExpiration = true
        };

        // Act
        await queryBus.SendAsync(query, cacheOptions); // First call - handler invoked
        await Task.Delay(300);
        await queryBus.SendAsync(query, cacheOptions); // Second call - from cache, extends expiration
        await Task.Delay(300);
        await queryBus.SendAsync(query, cacheOptions); // Third call - should still be in cache due to sliding

        // Assert
        // Handler should only be called once, the other two should be from cache
        counter.Count.Should().Be(1);
    }
}

// Test types
public record TestQuery : IQuery<string>
{
    public Guid QueryId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }
    public int Id { get; init; }
}

public class TestQueryHandler : IQueryHandler<TestQuery, string>
{
    public Task<string> HandleAsync(TestQuery query, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Result for {query.Id}");
    }
}

public class CallCounter
{
    public int Count { get; set; }
}

public class TestCountingQueryHandler : IQueryHandler<TestQuery, string>
{
    private readonly CallCounter _counter;

    public TestCountingQueryHandler(CallCounter counter)
    {
        _counter = counter;
    }

    public Task<string> HandleAsync(TestQuery query, CancellationToken cancellationToken = default)
    {
        _counter.Count++;
        return Task.FromResult($"Result for {query.Id}");
    }
}

public record UnhandledQuery : IQuery<string>
{
    public Guid QueryId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }
}
