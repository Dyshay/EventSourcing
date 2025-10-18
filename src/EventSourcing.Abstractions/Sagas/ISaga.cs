namespace EventSourcing.Abstractions.Sagas;

/// <summary>
/// Defines a saga that orchestrates a long-running business process
/// </summary>
/// <typeparam name="TData">The type of data passed between saga steps</typeparam>
public interface ISaga<TData> where TData : class
{
    /// <summary>
    /// Gets the unique identifier for this saga instance
    /// </summary>
    string SagaId { get; }

    /// <summary>
    /// Gets the name of this saga type
    /// </summary>
    string SagaName { get; }

    /// <summary>
    /// Gets the saga data context
    /// </summary>
    TData Data { get; }

    /// <summary>
    /// Gets the ordered list of steps in this saga
    /// </summary>
    IReadOnlyList<ISagaStep<TData>> Steps { get; }

    /// <summary>
    /// Gets the current status of the saga
    /// </summary>
    SagaStatus Status { get; }

    /// <summary>
    /// Gets the index of the current step being executed
    /// </summary>
    int CurrentStepIndex { get; }
}
