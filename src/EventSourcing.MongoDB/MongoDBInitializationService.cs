using EventSourcing.Abstractions;
using Microsoft.Extensions.Hosting;

namespace EventSourcing.MongoDB;

/// <summary>
/// Background service that initializes MongoDB storage on application startup.
/// </summary>
internal class MongoDBInitializationService : IHostedService
{
    private readonly IEventSourcingStorageProvider _provider;
    private readonly string[] _aggregateTypes;

    public MongoDBInitializationService(IEventSourcingStorageProvider provider, string[] aggregateTypes)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _aggregateTypes = aggregateTypes ?? throw new ArgumentNullException(nameof(aggregateTypes));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Validate configuration first
        _provider.ValidateConfiguration();

        // Initialize storage (create indexes, etc.)
        if (_aggregateTypes.Any())
        {
            await _provider.InitializeAsync(_aggregateTypes, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
