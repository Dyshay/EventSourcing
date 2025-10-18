using MediatR;

namespace EventSourcing.Core.StateMachine;

/// <summary>
/// Notification published when a state transition occurs.
/// Can be handled by MediatR notification handlers to react to state changes.
/// </summary>
/// <typeparam name="TState">The type of state</typeparam>
public record StateTransitionNotification<TState>(
    TState FromState,
    TState ToState,
    string AggregateType,
    string AggregateId
) : INotification where TState : struct, Enum;
