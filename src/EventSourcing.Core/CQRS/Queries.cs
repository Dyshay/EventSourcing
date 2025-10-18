using MediatR;

namespace EventSourcing.Core.CQRS;

/// <summary>
/// Base record for queries that retrieve data without modifying state.
/// </summary>
/// <typeparam name="TResult">The type of result returned</typeparam>
public abstract record Query<TResult> : IRequest<TResult>
{
    public Guid QueryId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
