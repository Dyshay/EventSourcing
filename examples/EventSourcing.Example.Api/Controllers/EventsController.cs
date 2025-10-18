using EventSourcing.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace EventSourcing.Example.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventStore _eventStore;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        IEventStore eventStore,
        ILogger<EventsController> logger)
    {
        _eventStore = eventStore;
        _logger = logger;
    }

    /// <summary>
    /// Get all events for a specific user
    /// </summary>
    [HttpGet("users/{userId}")]
    [ProducesResponseType(typeof(IEnumerable<EventEnvelope>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserEvents(Guid userId)
    {
        var events = await _eventStore.GetEventEnvelopesAsync(userId, "UserAggregate");

        _logger.LogInformation("Retrieved {Count} events for user {UserId}", events.Count(), userId);

        return Ok(events);
    }

    /// <summary>
    /// Get all events for all users (useful for audit trail, event replay, or building projections)
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IEnumerable<EventEnvelope>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUserEvents()
    {
        var events = await _eventStore.GetAllEventEnvelopesAsync("UserAggregate");

        _logger.LogInformation("Retrieved {Count} total events for all users", events.Count());

        return Ok(events);
    }

    /// <summary>
    /// Get all events since a specific timestamp (useful for incremental processing)
    /// </summary>
    [HttpGet("users/since")]
    [ProducesResponseType(typeof(IEnumerable<EventEnvelope>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserEventsSince([FromQuery] DateTimeOffset since)
    {
        var events = await _eventStore.GetAllEventEnvelopesAsync("UserAggregate", since);

        _logger.LogInformation("Retrieved {Count} events since {Timestamp}", events.Count(), since);

        return Ok(events);
    }

    /// <summary>
    /// Get all events of a specific kind/category (e.g., "user.created")
    /// </summary>
    [HttpGet("users/kind/{kind}")]
    [ProducesResponseType(typeof(IEnumerable<EventEnvelope>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserEventsByKind(string kind)
    {
        var events = await _eventStore.GetEventEnvelopesByKindAsync("UserAggregate", kind);

        _logger.LogInformation("Retrieved {Count} events of kind '{Kind}'", events.Count(), kind);

        return Ok(events);
    }

    /// <summary>
    /// Get all events matching any of the specified kinds (e.g., "user.created,user.updated")
    /// </summary>
    [HttpGet("users/kinds")]
    [ProducesResponseType(typeof(IEnumerable<EventEnvelope>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserEventsByKinds([FromQuery] string kinds)
    {
        var kindsList = kinds.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var events = await _eventStore.GetEventEnvelopesByKindsAsync("UserAggregate", kindsList);

        _logger.LogInformation("Retrieved {Count} events matching kinds: {Kinds}", events.Count(), string.Join(", ", kindsList));

        return Ok(events);
    }
}
