namespace EventSourcing.Abstractions.Sagas;

/// <summary>
/// Represents the status of a saga execution
/// </summary>
public enum SagaStatus
{
    /// <summary>
    /// Saga has not started yet
    /// </summary>
    NotStarted,

    /// <summary>
    /// Saga is currently executing
    /// </summary>
    Running,

    /// <summary>
    /// Saga completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Saga failed and is being compensated
    /// </summary>
    Compensating,

    /// <summary>
    /// Saga was compensated successfully
    /// </summary>
    Compensated,

    /// <summary>
    /// Saga compensation failed
    /// </summary>
    CompensationFailed
}
