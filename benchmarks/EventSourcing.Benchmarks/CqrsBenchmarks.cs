using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using EventSourcing.Abstractions;
using EventSourcing.Core;
using EventSourcing.CQRS.Commands;
using EventSourcing.CQRS.Configuration;
using EventSourcing.CQRS.Context;
using EventSourcing.CQRS.DependencyInjection;
using EventSourcing.CQRS.Queries;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventSourcing.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class CqrsBenchmarks
{
    private IServiceProvider _cqrsServices = null!;
    private IServiceProvider _cqrsHighPerfServices = null!;
    private IServiceProvider _mediatrServices = null!;
    private ICommandBus _commandBus = null!;
    private ICommandBus _commandBusHighPerf = null!;
    private IQueryBus _queryBus = null!;
    private IQueryBus _queryBusHighPerf = null!;
    private IMediator _mediator = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup CQRS (default with audit trail)
        var cqrsCollection = new ServiceCollection();
        cqrsCollection.AddLogging();
        cqrsCollection.AddCqrs(cqrs =>
        {
            cqrs.AddCommandHandler<TestCqrsCommand, TestEvent, TestCqrsCommandHandler>()
                .AddQueryHandler<TestCqrsQuery, string, TestCqrsQueryHandler>();
        });
        _cqrsServices = cqrsCollection.BuildServiceProvider();
        _commandBus = _cqrsServices.GetRequiredService<ICommandBus>();
        _queryBus = _cqrsServices.GetRequiredService<IQueryBus>();

        // Setup CQRS High Performance (no audit trail, no logging)
        var cqrsHighPerfCollection = new ServiceCollection();
        cqrsHighPerfCollection.AddLogging();
        cqrsHighPerfCollection.AddCqrs(
            cqrs =>
            {
                cqrs.AddCommandHandler<TestCqrsCommand, TestEvent, TestCqrsCommandHandler>()
                    .AddQueryHandler<TestCqrsQuery, string, TestCqrsQueryHandler>();
            },
            CqrsOptions.HighPerformance());
        _cqrsHighPerfServices = cqrsHighPerfCollection.BuildServiceProvider();
        _commandBusHighPerf = _cqrsHighPerfServices.GetRequiredService<ICommandBus>();
        _queryBusHighPerf = _cqrsHighPerfServices.GetRequiredService<IQueryBus>();

        // Setup MediatR
        var mediatrCollection = new ServiceCollection();
        mediatrCollection.AddLogging();
        mediatrCollection.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<TestMediatrCommand>();
        });
        _mediatrServices = mediatrCollection.BuildServiceProvider();
        _mediator = _mediatrServices.GetRequiredService<IMediator>();
    }

    [Benchmark(Baseline = true)]
    public async Task<string> MediatR_Command()
    {
        var command = new TestMediatrCommand { Value = "Test" };
        var result = await _mediator.Send(command);
        return result;
    }

    [Benchmark]
    public async Task<string> CQRS_Command()
    {
        var command = new TestCqrsCommand { Value = "Test" };
        var result = await _commandBus.SendAsync(command);
        return result.Data.Value;
    }

    [Benchmark]
    public async Task<string> MediatR_Query()
    {
        var query = new TestMediatrQuery { Id = 1 };
        var result = await _mediator.Send(query);
        return result;
    }

    [Benchmark]
    public async Task<string> CQRS_Query()
    {
        var query = new TestCqrsQuery { Id = 1 };
        var result = await _queryBus.SendAsync(query);
        return result;
    }

    [Benchmark]
    public async Task<string> CQRS_Query_WithCache()
    {
        var query = new TestCqrsQuery { Id = 1 };
        var cacheOptions = CacheOptions.WithDuration(TimeSpan.FromMinutes(5));
        var result = await _queryBus.SendAsync(query, cacheOptions);
        return result;
    }

    [Benchmark]
    public async Task<string> CQRS_Command_HighPerf()
    {
        var command = new TestCqrsCommand { Value = "Test" };
        var result = await _commandBusHighPerf.SendAsync(command);
        return result.Data.Value;
    }

    [Benchmark]
    public async Task<string> CQRS_Query_HighPerf()
    {
        var query = new TestCqrsQuery { Id = 1 };
        var result = await _queryBusHighPerf.SendAsync(query);
        return result;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_cqrsServices is IDisposable cqrsDisposable)
            cqrsDisposable.Dispose();
        if (_cqrsHighPerfServices is IDisposable cqrsHighPerfDisposable)
            cqrsHighPerfDisposable.Dispose();
        if (_mediatrServices is IDisposable mediatrDisposable)
            mediatrDisposable.Dispose();
    }
}

// Test event
public record TestEvent : DomainEvent
{
    public string Value { get; init; } = string.Empty;
}

// CQRS implementations
public record TestCqrsCommand : ICommand<TestEvent>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }
    public string Value { get; init; } = string.Empty;
}

public class TestCqrsCommandHandler : ICommandHandler<TestCqrsCommand, TestEvent>
{
    public Task<CommandResult<TestEvent>> HandleAsync(
        TestCqrsCommand command,
        CancellationToken cancellationToken = default)
    {
        var @event = new TestEvent { Value = command.Value };
        var result = CommandResult<TestEvent>.SuccessResult(@event);
        return Task.FromResult(result);
    }
}

public record TestCqrsQuery : IQuery<string>
{
    public Guid QueryId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }
    public int Id { get; init; }
}

public class TestCqrsQueryHandler : IQueryHandler<TestCqrsQuery, string>
{
    public Task<string> HandleAsync(TestCqrsQuery query, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Result for {query.Id}");
    }
}

// MediatR implementations
public record TestMediatrCommand : IRequest<string>
{
    public string Value { get; init; } = string.Empty;
}

public class TestMediatrCommandHandler : IRequestHandler<TestMediatrCommand, string>
{
    public Task<string> Handle(TestMediatrCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.Value);
    }
}

public record TestMediatrQuery : IRequest<string>
{
    public int Id { get; init; }
}

public class TestMediatrQueryHandler : IRequestHandler<TestMediatrQuery, string>
{
    public Task<string> Handle(TestMediatrQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Result for {request.Id}");
    }
}
