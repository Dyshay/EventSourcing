using EventSourcing.Abstractions;
using EventSourcing.Core.Projections;
using EventSourcing.Core.Publishing;
using EventSourcing.Core.StateMachine;
using EventSourcing.Example.Api.Domain;
using MediatR;

namespace EventSourcing.Example.Api.Infrastructure;

/// <summary>
/// Infrastructure bridge that converts domain events into MediatR notifications.
/// This keeps the domain layer pure while enabling reactive workflows via MediatR.
/// </summary>
public class MediatREventPublisher : IEventPublisher
{
    private readonly IMediator _mediator;
    private readonly ILogger<MediatREventPublisher> _logger;

    public MediatREventPublisher(
        IMediator mediator,
        ILogger<MediatREventPublisher> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        // Convert StateTransitionEvent<TState> domain events to MediatR notifications
        if (@event is StateTransitionEvent<OrderStatus> orderStateTransition)
        {
            var notification = new StateTransitionNotification<OrderStatus>(
                orderStateTransition.FromState,
                orderStateTransition.ToState,
                orderStateTransition.AggregateType,
                orderStateTransition.AggregateId
            );

            _logger.LogDebug(
                "Publishing MediatR notification for state transition: {FromState} → {ToState} (Aggregate: {AggregateId})",
                orderStateTransition.FromState,
                orderStateTransition.ToState,
                orderStateTransition.AggregateId
            );

            await _mediator.Publish(notification, cancellationToken);
        }

        // Add more event type conversions here as needed
        // if (@event is SomeOtherDomainEvent otherEvent) { ... }
    }
}
