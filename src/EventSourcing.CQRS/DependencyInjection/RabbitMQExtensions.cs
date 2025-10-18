using EventSourcing.CQRS.Events;
using EventSourcing.CQRS.Events.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.CQRS.DependencyInjection;

/// <summary>
/// Extension methods for configuring RabbitMQ event streaming
/// </summary>
public static class RabbitMQExtensions
{
    /// <summary>
    /// Registers RabbitMQ event stream publisher
    /// </summary>
    public static CqrsBuilder WithRabbitMQEventStreaming(
        this CqrsBuilder builder,
        Action<RabbitMQEventStreamOptions> configureOptions)
    {
        var options = new RabbitMQEventStreamOptions();
        configureOptions(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IEventStreamPublisher, RabbitMQEventStreamPublisher>();

        return builder;
    }

    /// <summary>
    /// Registers RabbitMQ event stream publisher with default options
    /// </summary>
    public static CqrsBuilder WithRabbitMQEventStreaming(this CqrsBuilder builder)
    {
        return builder.WithRabbitMQEventStreaming(options =>
        {
            // Use default options
        });
    }
}
