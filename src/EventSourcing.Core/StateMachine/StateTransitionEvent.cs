using EventSourcing.Abstractions;

namespace EventSourcing.Core.StateMachine;

/// <summary>
/// Domain event raised when a state transition occurs in a state machine.
/// This is a pure domain event with no infrastructure dependencies.
/// </summary>
/// <typeparam name="TState">The type of state (enum)</typeparam>
public record StateTransitionEvent<TState> : IEvent where TState : struct, Enum
{
    public TState FromState { get; init; }
    public TState ToState { get; init; }
    public string AggregateType { get; init; }
    public string AggregateId { get; init; }

    // IEvent implementation
    public Guid EventId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string EventType { get; init; }
    public string Kind { get; init; }

    public StateTransitionEvent(
        TState fromState,
        TState toState,
        string aggregateType,
        string aggregateId)
    {
        FromState = fromState;
        ToState = toState;
        AggregateType = aggregateType;
        AggregateId = aggregateId;

        // IEvent properties
        EventId = Guid.NewGuid();
        Timestamp = DateTimeOffset.UtcNow;
        EventType = $"StateTransitionEvent<{typeof(TState).Name}>";
        Kind = $"{aggregateType}.state_transition";
    }
}
