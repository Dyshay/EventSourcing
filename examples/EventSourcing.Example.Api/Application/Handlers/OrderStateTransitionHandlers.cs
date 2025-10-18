using EventSourcing.Core.StateMachine;
using EventSourcing.Example.Api.Domain;
using MediatR;

namespace EventSourcing.Example.Api.Application.Handlers;

/// <summary>
/// Example: React to order shipping by sending email notification.
/// This demonstrates reactive workflows triggered by state transitions.
/// </summary>
public class OrderShippedNotificationHandler
    : INotificationHandler<StateTransitionNotification<OrderStatus>>
{
    private readonly ILogger<OrderShippedNotificationHandler> _logger;

    public OrderShippedNotificationHandler(ILogger<OrderShippedNotificationHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(StateTransitionNotification<OrderStatus> notification, CancellationToken cancellationToken)
    {
        // Only react to transitions TO Shipped status
        if (notification.ToState != OrderStatus.Shipped)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Order {OrderId} has been shipped! Sending email notification to customer",
            notification.AggregateId);

        // In real app: Send email, push notification, update tracking system, etc.
        // await _emailService.SendOrderShippedEmail(notification.AggregateId);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Example: React to order cancellation by refunding payment.
/// </summary>
public class OrderCancelledRefundHandler
    : INotificationHandler<StateTransitionNotification<OrderStatus>>
{
    private readonly ILogger<OrderCancelledRefundHandler> _logger;

    public OrderCancelledRefundHandler(ILogger<OrderCancelledRefundHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(StateTransitionNotification<OrderStatus> notification, CancellationToken cancellationToken)
    {
        if (notification.ToState != OrderStatus.Cancelled)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Order {OrderId} was cancelled (from {FromStatus}). Initiating refund process",
            notification.AggregateId,
            notification.FromState);

        // In real app: Process refund via payment gateway
        // await _paymentService.RefundOrder(notification.AggregateId);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Example: Analytics handler that tracks all state transitions.
/// </summary>
public class OrderStateAnalyticsHandler
    : INotificationHandler<StateTransitionNotification<OrderStatus>>
{
    private readonly ILogger<OrderStateAnalyticsHandler> _logger;

    public OrderStateAnalyticsHandler(ILogger<OrderStateAnalyticsHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(StateTransitionNotification<OrderStatus> notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "State transition: Order {OrderId} moved from {FromState} to {ToState}",
            notification.AggregateId,
            notification.FromState,
            notification.ToState);

        // In real app: Send to analytics platform
        // await _analytics.TrackStateTransition(notification);

        return Task.CompletedTask;
    }
}
