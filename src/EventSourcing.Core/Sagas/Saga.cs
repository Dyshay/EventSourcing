using EventSourcing.Abstractions.Sagas;

namespace EventSourcing.Core.Sagas;

/// <summary>
/// Default implementation of a saga
/// </summary>
/// <typeparam name="TData">The type of data passed between saga steps</typeparam>
public class Saga<TData> : ISaga<TData> where TData : class
{
    private readonly List<ISagaStep<TData>> _steps = new();

    public string SagaId { get; }
    public string SagaName { get; }
    public TData Data { get; }
    public IReadOnlyList<ISagaStep<TData>> Steps => _steps.AsReadOnly();
    public SagaStatus Status { get; internal set; }
    public int CurrentStepIndex { get; internal set; }

    public Saga(string sagaName, TData data, string? sagaId = null)
    {
        SagaId = sagaId ?? Guid.NewGuid().ToString();
        SagaName = sagaName ?? throw new ArgumentNullException(nameof(sagaName));
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Status = SagaStatus.NotStarted;
        CurrentStepIndex = -1;
    }

    /// <summary>
    /// Adds a step to the saga
    /// </summary>
    public Saga<TData> AddStep(ISagaStep<TData> step)
    {
        if (step == null) throw new ArgumentNullException(nameof(step));
        if (Status != SagaStatus.NotStarted)
            throw new InvalidOperationException("Cannot add steps to a saga that has already started");

        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Adds multiple steps to the saga
    /// </summary>
    public Saga<TData> AddSteps(params ISagaStep<TData>[] steps)
    {
        foreach (var step in steps)
        {
            AddStep(step);
        }
        return this;
    }

    /// <summary>
    /// Restores saga state (used by saga stores for deserialization)
    /// </summary>
    public void RestoreState(SagaStatus status, int currentStepIndex)
    {
        Status = status;
        CurrentStepIndex = currentStepIndex;
    }
}
