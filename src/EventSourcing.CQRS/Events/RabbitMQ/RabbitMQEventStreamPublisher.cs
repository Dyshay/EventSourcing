using System.Text;
using System.Text.Json;
using EventSourcing.Abstractions;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EventSourcing.CQRS.Events.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of event stream publisher
/// </summary>
public class RabbitMQEventStreamPublisher : IEventStreamPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _exchangeName;
    private readonly ILogger<RabbitMQEventStreamPublisher> _logger;
    private bool _disposed;

    public RabbitMQEventStreamPublisher(
        RabbitMQEventStreamOptions options,
        ILogger<RabbitMQEventStreamPublisher> logger)
    {
        _logger = logger;
        _exchangeName = options.ExchangeName;

        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        // Declare the exchange (topic exchange for routing by event type)
        _channel.ExchangeDeclareAsync(
            exchange: _exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false).GetAwaiter().GetResult();

        _logger.LogInformation(
            "RabbitMQ Event Stream Publisher initialized. Exchange: {ExchangeName}, Host: {HostName}",
            _exchangeName,
            options.HostName);
    }

    public async Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RabbitMQEventStreamPublisher));

        try
        {
            var routingKey = GetRoutingKey(@event);
            var message = SerializeEvent(@event);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = @event.EventId.ToString(),
                Timestamp = new AmqpTimestamp(
                    @event.Timestamp.ToUnixTimeSeconds()),
                Type = @event.EventType,
                Headers = new Dictionary<string, object?>
                {
                    ["event-kind"] = @event.Kind,
                    ["event-type"] = @event.EventType
                }
            };

            await _channel.BasicPublishAsync(
                exchange: _exchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Published event {EventType} (ID: {EventId}) to RabbitMQ with routing key {RoutingKey}",
                @event.EventType,
                @event.EventId,
                routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error publishing event {EventType} to RabbitMQ",
                @event.EventType);
            throw;
        }
    }

    public async Task PublishBatchAsync(IEnumerable<IEvent> events, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RabbitMQEventStreamPublisher));

        var eventsList = events.ToList();

        _logger.LogDebug(
            "Publishing batch of {EventCount} events to RabbitMQ",
            eventsList.Count);

        try
        {
            // Publish each event in the batch
            foreach (var @event in eventsList)
            {
                await PublishAsync(@event, cancellationToken);
            }

            _logger.LogInformation(
                "Published batch of {EventCount} events to RabbitMQ",
                eventsList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error publishing event batch to RabbitMQ");
            throw;
        }
    }

    private static string GetRoutingKey(IEvent @event)
    {
        // Use event kind as routing key for topic-based routing
        // Examples: "order.created", "order.shipped", "order.cancelled"
        return @event.Kind.Replace(".", "_");
    }

    private static string SerializeEvent(IEvent @event)
    {
        return JsonSerializer.Serialize(@event, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _channel?.Dispose();
        _connection?.Dispose();

        _disposed = true;

        _logger.LogInformation("RabbitMQ Event Stream Publisher disposed");
    }
}

/// <summary>
/// Configuration options for RabbitMQ event stream publisher
/// </summary>
public class RabbitMQEventStreamOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "events";
}
