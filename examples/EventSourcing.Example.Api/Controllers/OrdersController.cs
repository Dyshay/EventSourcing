using EventSourcing.Abstractions;
using EventSourcing.Example.Api.Domain;
using EventSourcing.Example.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventSourcing.Example.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IAggregateRepository<OrderAggregate, Guid> _repository;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IAggregateRepository<OrderAggregate, Guid> repository,
        ILogger<OrdersController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all orders
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllOrders()
    {
        var eventStore = HttpContext.RequestServices.GetRequiredService<IEventStore>();
        var aggregateIds = await eventStore.GetAllAggregateIdsAsync("OrderAggregate");

        var orders = new List<OrderResponse>();

        foreach (var aggregateIdStr in aggregateIds)
        {
            try
            {
                var aggregateId = Guid.Parse(aggregateIdStr);
                var order = await _repository.GetByIdAsync(aggregateId);
                orders.Add(MapToResponse(order));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load order with ID {AggregateId}", aggregateIdStr);
            }
        }

        _logger.LogInformation("Retrieved {Count} orders", orders.Count);

        return Ok(orders);
    }

    /// <summary>
    /// Create a new order
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            var orderId = Guid.NewGuid();

            // Create aggregate (pure domain, no infrastructure dependencies)
            var order = new OrderAggregate();

            order.CreateOrder(orderId, request.CustomerId);
            await _repository.SaveAsync(order);

            _logger.LogInformation("Order created with ID: {OrderId}", orderId);

            var response = MapToResponse(order);
            return CreatedAtAction(nameof(GetOrder), new { id = orderId }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get order by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(Guid id)
    {
        try
        {
            var order = await _repository.GetByIdAsync(id);
            var response = MapToResponse(order);
            return Ok(response);
        }
        catch (AggregateNotFoundException)
        {
            return NotFound(new { error = $"Order with ID {id} not found" });
        }
    }

    /// <summary>
    /// Add item to order
    /// </summary>
    [HttpPost("{id}/items")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddOrderItemRequest request)
    {
        try
        {
            var order = await _repository.GetByIdAsync(id);
            order.AddItem(request.ProductName, request.Quantity, request.UnitPrice);
            await _repository.SaveAsync(order);

            _logger.LogInformation("Item added to order {OrderId}: {ProductName}", id, request.ProductName);

            var response = MapToResponse(order);
            return Ok(response);
        }
        catch (AggregateNotFoundException)
        {
            return NotFound(new { error = $"Order with ID {id} not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Ship order
    /// </summary>
    [HttpPost("{id}/ship")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ShipOrder(Guid id, [FromBody] ShipOrderRequest request)
    {
        try
        {
            var order = await _repository.GetByIdAsync(id);

            // Execute business logic (pure domain)
            // State machine validates + emits StateTransitionEvent
            order.Ship(request.ShippingAddress, request.TrackingNumber);

            // Save (publishes events via IEventBus → MediatREventPublisher)
            await _repository.SaveAsync(order);

            _logger.LogInformation("Order {OrderId} shipped to {Address}", id, request.ShippingAddress);

            var response = MapToResponse(order);
            return Ok(response);
        }
        catch (AggregateNotFoundException)
        {
            return NotFound(new { error = $"Order with ID {id} not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel order
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelOrder(Guid id, [FromBody] CancelOrderRequest request)
    {
        try
        {
            var order = await _repository.GetByIdAsync(id);

            // Execute business logic (pure domain)
            // State machine validates + emits StateTransitionEvent
            order.Cancel(request.Reason);

            // Save (publishes events via IEventBus → MediatREventPublisher)
            await _repository.SaveAsync(order);

            _logger.LogInformation("Order {OrderId} cancelled: {Reason}", id, request.Reason);

            var response = MapToResponse(order);
            return Ok(response);
        }
        catch (AggregateNotFoundException)
        {
            return NotFound(new { error = $"Order with ID {id} not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all events for a specific order (event history/audit trail)
    /// </summary>
    [HttpGet("{id}/events")]
    [ProducesResponseType(typeof(IEnumerable<EventEnvelope>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderEvents(Guid id)
    {
        // Check if order exists
        if (!await _repository.ExistsAsync(id))
        {
            return NotFound(new { error = $"Order with ID {id} not found" });
        }

        var eventStore = HttpContext.RequestServices.GetRequiredService<IEventStore>();
        var events = await eventStore.GetEventEnvelopesAsync(id, "OrderAggregate");

        _logger.LogInformation("Retrieved {Count} events for order {OrderId}", events.Count(), id);

        return Ok(events);
    }

    private static OrderResponse MapToResponse(OrderAggregate order)
    {
        var items = order.Items.Select(item => new OrderItemResponse(
            item.ProductName,
            item.Quantity,
            item.UnitPrice,
            item.Quantity * item.UnitPrice
        )).ToList();

        return new OrderResponse(
            order.Id,
            order.CustomerId,
            order.Total,
            items,
            order.Status,
            order.ShippingAddress,
            order.TrackingNumber,
            order.CancellationReason,
            order.Version
        );
    }
}
