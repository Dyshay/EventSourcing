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
public class AggregateRepository<TAggregate, TId>(
    IEventStore eventStore,
    ISnapshotStore snapshotStore,
    ISnapshotStrategy snapshotStrategy,
    IEventBus? eventBus = null)
    : IAggregateRepository<TAggregate, TId>
    where TAggregate : IAggregate<TId>, new()
    where TId : notnull
{
    private readonly IEventStore _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    private readonly ISnapshotStore _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
    private readonly ISnapshotStrategy _snapshotStrategy = snapshotStrategy ?? throw new ArgumentNullException(nameof(snapshotStrategy));

    // Optional - only used if projections/publishers are configured
    private readonly string _aggregateType = typeof(TAggregate).Name;

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
        if (eventBus != null)
        {
            await eventBus.PublishAsync(uncommittedEvents, cancellationToken);
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

    public async Task<AppendEventsResult> SaveWithResultAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var uncommittedEvents = aggregate.UncommittedEvents.ToList();

        if (!uncommittedEvents.Any())
        {
            // Nothing to save, return empty result
            return AppendEventsResult.Empty(aggregate.Version);
        }

        // Append events to event store and get result with event IDs
        var result = await _eventStore.AppendEventsWithResultAsync(
            aggregate.Id,
            _aggregateType,
            uncommittedEvents,
            aggregate.Version,
            cancellationToken);

        if (result == null)
        {
            throw new InvalidOperationException("Event store returned null result from AppendEventsWithResultAsync");
        }

        // Mark events as committed
        aggregate.MarkEventsAsCommitted();
        aggregate.Version = result.NewVersion;

        // Publish events to projections and external publishers (if configured)
        if (eventBus != null)
        {
            await eventBus.PublishAsync(uncommittedEvents, cancellationToken);
        }

        // Check if we should create a snapshot
        var snapshot = await _snapshotStore.GetLatestSnapshotAsync<TId, TAggregate>(
            aggregate.Id,
            _aggregateType,
            cancellationToken);

        var eventCountSinceSnapshot = snapshot != null
            ? result.NewVersion - snapshot.Version
            : result.NewVersion;

        if (_snapshotStrategy.ShouldCreateSnapshot(aggregate, eventCountSinceSnapshot, snapshot?.Timestamp))
        {
            await _snapshotStore.SaveSnapshotAsync(
                aggregate.Id,
                _aggregateType,
                aggregate,
                result.NewVersion,
                cancellationToken);
        }

        // Return result with both event IDs and the actual events
        return new AppendEventsResult(result.EventIds, uncommittedEvents, result.NewVersion);
    }

    public async Task<Guid> AppendEventAsync(TId aggregateId, IEvent @event, int expectedVersion, CancellationToken cancellationToken = default)
    {
        // Append the single event directly to the event store
        var eventId = await _eventStore.AppendEventAsync(
            aggregateId,
            _aggregateType,
            @event,
            expectedVersion,
            cancellationToken);

        // Publish the event to projections and external publishers (if configured)
        if (eventBus != null)
        {
            await eventBus.PublishAsync(new[] { @event }, cancellationToken);
        }

        // Check if we should create a snapshot
        var newVersion = expectedVersion + 1;
        var snapshot = await _snapshotStore.GetLatestSnapshotAsync<TId, TAggregate>(
            aggregateId,
            _aggregateType,
            cancellationToken);

        var eventCountSinceSnapshot = snapshot != null
            ? newVersion - snapshot.Version
            : newVersion;

        // Only create snapshot if we have an aggregate instance (would need to load it)
        // For now, we skip snapshot creation for direct event appends
        // as it would require loading the aggregate which defeats the purpose

        return eventId;
    }

    public async Task<PagedResult<TAggregate>> GetAllPaginatedAsync(
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        // Récupérer les IDs paginés
        var paginatedIds = await _eventStore.GetAggregateIdsPaginatedAsync(
            _aggregateType,
            pageNumber,
            pageSize,
            cancellationToken);

        if (!paginatedIds.Items.Any())
        {
            return PagedResult<TAggregate>.Empty(pageNumber, pageSize);
        }

        // Charger chaque agrégat
        var aggregates = new List<TAggregate>();

        foreach (var aggregateIdStr in paginatedIds.Items)
        {
            try
            {
                // Convertir l'ID string vers le type TId
                var aggregateId = ConvertToTypedId(aggregateIdStr);
                var aggregate = await GetByIdAsync(aggregateId, cancellationToken);
                aggregates.Add(aggregate);
            }
            catch (AggregateNotFoundException)
            {
                // Ignorer les agrégats qui ne peuvent pas être chargés
                // (cela peut arriver si des événements sont corrompus)
                continue;
            }
        }

        return new PagedResult<TAggregate>(aggregates, pageNumber, pageSize, paginatedIds.TotalCount);
    }

    /// <summary>
    /// Converts a string ID to the typed ID.
    /// </summary>
    private TId ConvertToTypedId(string aggregateIdStr)
    {
        var idType = typeof(TId);

        try
        {
            if (idType == typeof(Guid))
            {
                return (TId)(object)Guid.Parse(aggregateIdStr);
            }
            else if (idType == typeof(string))
            {
                return (TId)(object)aggregateIdStr;
            }
            else if (idType == typeof(int))
            {
                return (TId)(object)int.Parse(aggregateIdStr);
            }
            else if (idType == typeof(long))
            {
                return (TId)(object)long.Parse(aggregateIdStr);
            }
            else
            {
                // Pour d'autres types, essayer la conversion standard
                return (TId)Convert.ChangeType(aggregateIdStr, idType);
            }
        }
        catch (Exception ex)
        {
            throw new AggregateNotFoundException($"Invalid aggregate ID format: '{aggregateIdStr}' cannot be converted to {idType.Name}. {ex.Message}");
        }
    }
}
