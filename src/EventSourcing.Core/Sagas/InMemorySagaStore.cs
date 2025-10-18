using System.Collections.Concurrent;
using System.Text.Json;
using EventSourcing.Abstractions.Sagas;

namespace EventSourcing.Core.Sagas;

/// <summary>
/// In-memory implementation of saga store for testing and development
/// </summary>
public class InMemorySagaStore : ISagaStore
{
    private readonly ConcurrentDictionary<string, string> _sagas = new();

    public Task SaveAsync<TData>(ISaga<TData> saga, CancellationToken cancellationToken = default)
        where TData : class
    {
        if (saga == null) throw new ArgumentNullException(nameof(saga));

        var sagaState = new SagaState<TData>
        {
            SagaId = saga.SagaId,
            SagaName = saga.SagaName,
            Data = saga.Data,
            Status = saga.Status,
            CurrentStepIndex = saga.CurrentStepIndex,
            DataType = typeof(TData).AssemblyQualifiedName!
        };

        var json = JsonSerializer.Serialize(sagaState);
        _sagas[saga.SagaId] = json;

        return Task.CompletedTask;
    }

    public Task<ISaga<TData>?> LoadAsync<TData>(string sagaId, CancellationToken cancellationToken = default)
        where TData : class
    {
        if (string.IsNullOrEmpty(sagaId)) throw new ArgumentNullException(nameof(sagaId));

        if (!_sagas.TryGetValue(sagaId, out var json))
            return Task.FromResult<ISaga<TData>?>(null);

        var sagaState = JsonSerializer.Deserialize<SagaState<TData>>(json);
        if (sagaState == null)
            return Task.FromResult<ISaga<TData>?>(null);

        var saga = new Saga<TData>(sagaState.SagaName, sagaState.Data, sagaState.SagaId);
        saga.RestoreState(sagaState.Status, sagaState.CurrentStepIndex);

        return Task.FromResult<ISaga<TData>?>(saga);
    }

    public Task DeleteAsync(string sagaId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sagaId)) throw new ArgumentNullException(nameof(sagaId));

        _sagas.TryRemove(sagaId, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Internal class for serializing saga state
    /// </summary>
    private class SagaState<TData> where TData : class
    {
        public string SagaId { get; set; } = string.Empty;
        public string SagaName { get; set; } = string.Empty;
        public TData Data { get; set; } = null!;
        public SagaStatus Status { get; set; }
        public int CurrentStepIndex { get; set; }
        public string DataType { get; set; } = string.Empty;
    }
}
