using MediatR;

namespace EventSourcing.Core.StateMachine;

/// <summary>
/// State machine that publishes MediatR notifications on state transitions.
/// Extends StateMachine with MediatR integration for reactive workflows.
/// </summary>
/// <typeparam name="TState">The type of state (usually an enum)</typeparam>
public class StateMachineWithMediatr<TState> : StateMachine<TState> where TState : struct, Enum
{
    private readonly IMediator? _mediator;
    private readonly string _aggregateType;
    private readonly Func<string> _getAggregateId;

    public StateMachineWithMediatr(
        TState initialState,
        IMediator? mediator,
        string aggregateType,
        Func<string> getAggregateId)
        : base(initialState)
    {
        _mediator = mediator;
        _aggregateType = aggregateType;
        _getAggregateId = getAggregateId;
    }

    /// <summary>
    /// Transitions to a new state and publishes a MediatR notification.
    /// </summary>
    public async Task TransitionToAsync(TState newState, CancellationToken cancellationToken = default)
    {
        if (CurrentState.Equals(newState))
        {
            return; // Already in target state
        }

        var fromState = CurrentState;

        // Perform the transition (base class handles validation and hooks)
        TransitionTo(newState);

        // Publish notification if mediator is available
        if (_mediator != null)
        {
            var notification = new StateTransitionNotification<TState>(
                fromState,
                newState,
                _aggregateType,
                _getAggregateId()
            );

            await _mediator.Publish(notification, cancellationToken);
        }
    }
}
