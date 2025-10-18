using EventSourcing.Abstractions;
using EventSourcing.Core.Publishing;
using EventSourcing.Core.Snapshots;

namespace EventSourcing.Core;

/// <summary>
/// Repository for managing aggregate lifecycle.
/// Orchestrates event store, snapshot store, snapshot strategy, and event publishing.
/// </summary>
/// <typeparam name="TAggregate">Type of the aggregate</typeparam>
/// <typeparam name="TId">Type of the aggregate identifier</typeparam>
public class AggregateRepository<TAggregate, TId> : IAggregateRepository<TAggregate, TId>
    where TAggregate : IAggregate<TId>, new()
    where TId : notnull
{
    private readonly IEventStore _eventStore;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ISnapshotStrategy _snapshotStrategy;
    private readonly IEventBus? _eventBus;
    private readonly string _aggregateType;

    public AggregateRepository(
        IEventStore eventStore,
        ISnapshotStore snapshotStore,
        ISnapshotStrategy snapshotStrategy,
        IEventBus? eventBus = null)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _snapshotStrategy = snapshotStrategy ?? throw new ArgumentNullException(nameof(snapshotStrategy));
        _eventBus = eventBus; // Optional - only used if projections/publishers are configured
        _aggregateType = typeof(TAggregate).Name;
    }

    public async Task<TAggregate> GetByIdAsync(TId aggregateId, CancellationToken cancellationToken = default)
    {
        // Try to load from snapshot first
        var snapshot = await _snapshotStore.GetLatestSnapshotAsync<TId, TAggregate>(
            aggregateId,
            _aggregateType,
            cancellationToken);

        TAggregate aggregate;
        int fromVersion = 0;

        if (snapshot != null)
        {
            // Start from snapshot
            aggregate = snapshot.Aggregate;
            fromVersion = snapshot.Version;
        }
        else
        {
            // No snapshot, start from scratch
            aggregate = new TAggregate();
        }

        // Load events since snapshot (or from beginning if no snapshot)
        var events = await _eventStore.GetEventsAsync(
            aggregateId,
            _aggregateType,
            fromVersion,
            cancellationToken);

        var eventsList = events.ToList();

        if (!eventsList.Any() && snapshot == null)
        {
            // Aggregate doesn't exist
            throw new AggregateNotFoundException(aggregateId, typeof(TAggregate));
        }

        // Replay events to reconstruct current state
        if (eventsList.Any())
        {
            aggregate.LoadFromHistory(eventsList);
        }

        return aggregate;
    }

    public async Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var uncommittedEvents = aggregate.UncommittedEvents.ToList();

        if (!uncommittedEvents.Any())
        {
            // Nothing to save
            return;
        }

        // Append events to event store
        await _eventStore.AppendEventsAsync(
            aggregate.Id,
            _aggregateType,
            uncommittedEvents,
            aggregate.Version,
            cancellationToken);

        // Update version
        var newVersion = aggregate.Version + uncommittedEvents.Count;

        // Mark events as committed
        aggregate.MarkEventsAsCommitted();
        aggregate.Version = newVersion;

        // Publish events to projections and external publishers (if configured)
        if (_eventBus != null)
        {
            await _eventBus.PublishAsync(uncommittedEvents, cancellationToken);
        }

        // Check if we should create a snapshot
        var snapshot = await _snapshotStore.GetLatestSnapshotAsync<TId, TAggregate>(
            aggregate.Id,
            _aggregateType,
            cancellationToken);

        var eventCountSinceSnapshot = snapshot != null
            ? newVersion - snapshot.Version
            : newVersion;

        if (_snapshotStrategy.ShouldCreateSnapshot(aggregate, eventCountSinceSnapshot, snapshot?.Timestamp))
        {
            await _snapshotStore.SaveSnapshotAsync(
                aggregate.Id,
                _aggregateType,
                aggregate,
                newVersion,
                cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(TId aggregateId, CancellationToken cancellationToken = default)
    {
        try
        {
            await GetByIdAsync(aggregateId, cancellationToken);
            return true;
        }
        catch (AggregateNotFoundException)
        {
            return false;
        }
    }
}
