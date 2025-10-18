using EventSourcing.Abstractions.Sagas;

namespace EventSourcing.Core.Sagas;

/// <summary>
/// Base class for implementing saga steps
/// </summary>
/// <typeparam name="TData">The type of saga data</typeparam>
public abstract class SagaStepBase<TData> : ISagaStep<TData> where TData : class
{
    public abstract string Name { get; }

    public abstract Task<bool> ExecuteAsync(TData data, CancellationToken cancellationToken = default);

    public abstract Task<bool> CompensateAsync(TData data, CancellationToken cancellationToken = default);
}
