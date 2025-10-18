using EventSourcing.CQRS.Commands;
using EventSourcing.CQRS.Queries;
using EventSourcing.Example.Api.Application.Cqrs.Commands;
using EventSourcing.Example.Api.Application.Cqrs.Queries;
using Microsoft.AspNetCore.Mvc;

namespace EventSourcing.Example.Api.Controllers;

/// <summary>
/// Controller demonstrating the new CQRS framework usage
/// </summary>
[ApiController]
[Route("api/v2/orders")]
[Produces("application/json")]
public class OrdersCqrsController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly ILogger<OrdersCqrsController> _logger;

    public OrdersCqrsController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        ILogger<OrdersCqrsController> logger)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new order using CQRS command bus
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCqrsRequest request)
    {
        var command = new CreateOrderCqrsCommand
        {
            CustomerId = Guid.TryParse(request.CustomerId, out var customerId) ? customerId : Guid.Empty,
            Metadata = new Dictionary<string, object>
            {
                ["UserId"] = User?.Identity?.Name ?? "Anonymous",
                ["IpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            }
        };

        var result = await _commandBus.SendAsync(command);

        if (!result.Success)
        {
            _logger.LogWarning("Failed to create order: {ErrorMessage}", result.ErrorMessage);
            return BadRequest(new { error = result.ErrorMessage });
        }

        _logger.LogInformation(
            "Order {OrderId} created successfully in {ExecutionTime}ms",
            result.AggregateId,
            result.ExecutionTimeMs);

        return CreatedAtAction(
            nameof(GetOrder),
            new { id = result.AggregateId },
            new
            {
                orderId = result.AggregateId,
                @event = result.Data,
                version = result.Version,
                executionTimeMs = result.ExecutionTimeMs
            });
    }

    /// <summary>
    /// Gets an order by ID with caching
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(Guid id)
    {
        var query = new GetOrderCqrsQuery { OrderId = id };

        // Use cache for 5 minutes with sliding expiration
        var cacheOptions = CacheOptions.WithDuration(
            TimeSpan.FromMinutes(5),
            sliding: true);

        var order = await _queryBus.SendAsync(query, cacheOptions);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", id);
            return NotFound(new { error = $"Order {id} not found" });
        }

        return Ok(order);
    }

    /// <summary>
    /// Gets order status (cached and invalidated on order events)
    /// </summary>
    [HttpGet("{id}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderStatus(Guid id)
    {
        var query = new GetOrderStatusCqrsQuery { OrderId = id };

        // Cache with automatic invalidation when order events occur
        var cacheOptions = new CacheOptions
        {
            CacheKey = $"order-status-{id}",
            Duration = TimeSpan.FromMinutes(10),
            InvalidateOnEvents = new[]
            {
                "OrderCreatedEvent",
                "OrderItemAddedEvent",
                "OrderShippedEvent",
                "OrderCancelledEvent"
            }
        };

        var status = await _queryBus.SendAsync(query, cacheOptions);

        if (status == null)
        {
            _logger.LogWarning("Order {OrderId} not found", id);
            return NotFound(new { error = $"Order {id} not found" });
        }

        return Ok(status);
    }

    /// <summary>
    /// Gets allowed actions for an order
    /// </summary>
    [HttpGet("{id}/actions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllowedActions(Guid id)
    {
        var query = new GetAllowedOrderActionsCqrsQuery { OrderId = id };
        var actions = await _queryBus.SendAsync(query);

        return Ok(actions);
    }

    /// <summary>
    /// Gets order state at a specific point in time (temporal query)
    /// </summary>
    [HttpGet("{id}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderHistory(Guid id, [FromQuery] DateTimeOffset asOfDate)
    {
        var query = new GetOrderAsOfDateQuery
        {
            OrderId = id,
            AsOfDate = asOfDate
        };

        var order = await _queryBus.SendAsync(query);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found at {AsOfDate}", id, asOfDate);
            return NotFound(new { error = $"Order {id} not found at {asOfDate}" });
        }

        return Ok(order);
    }

    /// <summary>
    /// Adds an item to an order
    /// </summary>
    [HttpPost("{id}/items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddOrderItemCqrsRequest request)
    {
        var command = new AddOrderItemCqrsCommand
        {
            OrderId = id,
            ProductName = request.ProductId,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice
        };

        var result = await _commandBus.SendAsync(command);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Failed to add item to order {OrderId}: {ErrorMessage}",
                id,
                result.ErrorMessage);
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new
        {
            @event = result.Data,
            version = result.Version,
            executionTimeMs = result.ExecutionTimeMs
        });
    }

    /// <summary>
    /// Ships an order
    /// </summary>
    [HttpPost("{id}/ship")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ShipOrder(Guid id, [FromBody] ShipOrderCqrsRequest request)
    {
        var command = new ShipOrderCqrsCommand
        {
            OrderId = id,
            TrackingNumber = request.TrackingNumber
        };

        var result = await _commandBus.SendAsync(command);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Failed to ship order {OrderId}: {ErrorMessage}",
                id,
                result.ErrorMessage);
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new
        {
            @event = result.Data,
            version = result.Version,
            executionTimeMs = result.ExecutionTimeMs
        });
    }

    /// <summary>
    /// Cancels an order
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelOrder(Guid id, [FromBody] CancelOrderCqrsRequest request)
    {
        var command = new CancelOrderCqrsCommand
        {
            OrderId = id,
            Reason = request.Reason
        };

        var result = await _commandBus.SendAsync(command);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Failed to cancel order {OrderId}: {ErrorMessage}",
                id,
                result.ErrorMessage);
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new
        {
            @event = result.Data,
            version = result.Version,
            executionTimeMs = result.ExecutionTimeMs
        });
    }
}

// Request DTOs (v2 CQRS-specific)
public record CreateOrderCqrsRequest
{
    public string CustomerId { get; init; } = string.Empty;
    public string ShippingAddress { get; init; } = string.Empty;
}

public record AddOrderItemCqrsRequest
{
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}

public record ShipOrderCqrsRequest
{
    public string TrackingNumber { get; init; } = string.Empty;
}

public record CancelOrderCqrsRequest
{
    public string Reason { get; init; } = string.Empty;
}
