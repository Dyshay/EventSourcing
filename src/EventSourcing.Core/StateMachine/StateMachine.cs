namespace EventSourcing.Core.StateMachine;

/// <summary>
/// Generic state machine for managing state transitions in aggregates.
/// Validates transitions and provides hooks for state changes.
/// </summary>
/// <typeparam name="TState">The type of state (usually an enum)</typeparam>
public class StateMachine<TState> where TState : struct, Enum
{
    private readonly Dictionary<TState, HashSet<TState>> _allowedTransitions = new();
    private readonly Dictionary<TState, List<Action>> _onEnterActions = new();
    private readonly Dictionary<TState, List<Action>> _onExitActions = new();

    public TState CurrentState { get; private set; }
    public TState? PreviousState { get; private set; }

    public StateMachine(TState initialState)
    {
        CurrentState = initialState;
    }

    /// <summary>
    /// Defines an allowed transition from one state to another.
    /// </summary>
    public StateMachine<TState> Allow(TState from, TState to)
    {
        if (!_allowedTransitions.ContainsKey(from))
        {
            _allowedTransitions[from] = new HashSet<TState>();
        }

        _allowedTransitions[from].Add(to);
        return this;
    }

    /// <summary>
    /// Defines multiple allowed transitions from one state.
    /// </summary>
    public StateMachine<TState> Allow(TState from, params TState[] toStates)
    {
        foreach (var to in toStates)
        {
            Allow(from, to);
        }
        return this;
    }

    /// <summary>
    /// Registers an action to execute when entering a state.
    /// </summary>
    public StateMachine<TState> OnEnter(TState state, Action action)
    {
        if (!_onEnterActions.ContainsKey(state))
        {
            _onEnterActions[state] = new List<Action>();
        }

        _onEnterActions[state].Add(action);
        return this;
    }

    /// <summary>
    /// Registers an action to execute when exiting a state.
    /// </summary>
    public StateMachine<TState> OnExit(TState state, Action action)
    {
        if (!_onExitActions.ContainsKey(state))
        {
            _onExitActions[state] = new List<Action>();
        }

        _onExitActions[state].Add(action);
        return this;
    }

    /// <summary>
    /// Checks if a transition is allowed without executing it.
    /// </summary>
    public bool CanTransitionTo(TState newState)
    {
        if (CurrentState.Equals(newState))
        {
            return true; // Already in target state
        }

        return _allowedTransitions.ContainsKey(CurrentState) &&
               _allowedTransitions[CurrentState].Contains(newState);
    }

    /// <summary>
    /// Executes a state transition. Throws if transition is not allowed.
    /// </summary>
    public void TransitionTo(TState newState)
    {
        if (CurrentState.Equals(newState))
        {
            return; // Already in target state
        }

        if (!CanTransitionTo(newState))
        {
            throw new InvalidStateTransitionException(
                CurrentState.ToString()!,
                newState.ToString()!,
                $"Transition from {CurrentState} to {newState} is not allowed");
        }

        // Execute exit actions for current state
        if (_onExitActions.ContainsKey(CurrentState))
        {
            foreach (var action in _onExitActions[CurrentState])
            {
                action();
            }
        }

        // Update state
        PreviousState = CurrentState;
        CurrentState = newState;

        // Execute enter actions for new state
        if (_onEnterActions.ContainsKey(CurrentState))
        {
            foreach (var action in _onEnterActions[CurrentState])
            {
                action();
            }
        }
    }

    /// <summary>
    /// Forces a state change without validation. Used when replaying events.
    /// </summary>
    public void SetState(TState newState)
    {
        if (!CurrentState.Equals(newState))
        {
            PreviousState = CurrentState;
        }
        CurrentState = newState;
    }

    /// <summary>
    /// Gets all allowed transitions from the current state.
    /// </summary>
    public IEnumerable<TState> GetAllowedTransitions()
    {
        if (_allowedTransitions.ContainsKey(CurrentState))
        {
            return _allowedTransitions[CurrentState];
        }

        return Enumerable.Empty<TState>();
    }
}

/// <summary>
/// Exception thrown when an invalid state transition is attempted.
/// </summary>
public class InvalidStateTransitionException : InvalidOperationException
{
    public string FromState { get; }
    public string ToState { get; }

    public InvalidStateTransitionException(string fromState, string toState, string message)
        : base(message)
    {
        FromState = fromState;
        ToState = toState;
    }
}
