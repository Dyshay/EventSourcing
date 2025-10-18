namespace EventSourcing.Core.StateMachine;

/// <summary>
/// State machine that emits domain events on state transitions.
/// Pure domain logic with no infrastructure dependencies.
/// </summary>
/// <typeparam name="TState">The type of state (usually an enum)</typeparam>
public class StateMachineWithEvents<TState> : StateMachine<TState> where TState : struct, Enum
{
    private readonly Action<StateTransitionEvent<TState>>? _onTransition;
    private readonly string _aggregateType;
    private readonly Func<string> _getAggregateId;

    public StateMachineWithEvents(
        TState initialState,
        string aggregateType,
        Func<string> getAggregateId,
        Action<StateTransitionEvent<TState>>? onTransition = null)
        : base(initialState)
    {
        _aggregateType = aggregateType;
        _getAggregateId = getAggregateId;
        _onTransition = onTransition;
    }

    /// <summary>
    /// Transitions to a new state and raises a domain event.
    /// </summary>
    public void TransitionToWithEvent(TState newState)
    {
        if (CurrentState.Equals(newState))
        {
            return; // Already in target state
        }

        var fromState = CurrentState;

        // Perform the transition (base class handles validation and hooks)
        TransitionTo(newState);

        // Raise domain event if callback is provided
        if (_onTransition != null)
        {
            var domainEvent = new StateTransitionEvent<TState>(
                fromState,
                newState,
                _aggregateType,
                _getAggregateId()
            );

            _onTransition(domainEvent);
        }
    }
}
