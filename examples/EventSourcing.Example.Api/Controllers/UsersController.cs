using EventSourcing.Abstractions;
using EventSourcing.Example.Api.Domain;
using EventSourcing.Example.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventSourcing.Example.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IAggregateRepository<UserAggregate, Guid> _repository;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IAggregateRepository<UserAggregate, Guid> repository,
        ILogger<UsersController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers()
    {
        var eventStore = HttpContext.RequestServices.GetRequiredService<IEventStore>();
        var aggregateIds = await eventStore.GetAllAggregateIdsAsync("UserAggregate");

        var users = new List<UserResponse>();

        foreach (var aggregateIdStr in aggregateIds)
        {
            try
            {
                var aggregateId = Guid.Parse(aggregateIdStr);
                var user = await _repository.GetByIdAsync(aggregateId);
                users.Add(MapToResponse(user));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load user with ID {AggregateId}", aggregateIdStr);
            }
        }

        _logger.LogInformation("Retrieved {Count} users", users.Count);

        return Ok(users);
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var userId = Guid.NewGuid();
            var user = new UserAggregate();

            user.CreateUser(userId, request.Email, request.FirstName, request.LastName);
            await _repository.SaveAsync(user);

            _logger.LogInformation("User created with ID: {UserId}", userId);

            var response = MapToResponse(user);
            return CreatedAtAction(nameof(GetUser), new { id = userId }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid id)
    {
        try
        {
            var user = await _repository.GetByIdAsync(id);
            var response = MapToResponse(user);
            return Ok(response);
        }
        catch (AggregateNotFoundException)
        {
            return NotFound(new { error = $"User with ID {id} not found" });
        }
    }

    /// <summary>
    /// Update user name
    /// </summary>
    [HttpPut("{id}/name")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUserName(Guid id, [FromBody] UpdateUserNameRequest request)
    {
        try
        {
            var user = await _repository.GetByIdAsync(id);
            user.ChangeName(request.FirstName, request.LastName);
            await _repository.SaveAsync(user);

            _logger.LogInformation("User name updated for ID: {UserId}", id);

            var response = MapToResponse(user);
            return Ok(response);
        }
        catch (AggregateNotFoundException)
        {
            return NotFound(new { error = $"User with ID {id} not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update user email
    /// </summary>
    [HttpPut("{id}/email")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUserEmail(Guid id, [FromBody] UpdateUserEmailRequest request)
    {
        try
        {
            var user = await _repository.GetByIdAsync(id);
            user.ChangeEmail(request.Email);
            await _repository.SaveAsync(user);

            _logger.LogInformation("User email updated for ID: {UserId}", id);

            var response = MapToResponse(user);
            return Ok(response);
        }
        catch (AggregateNotFoundException)
        {
            return NotFound(new { error = $"User with ID {id} not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Activate user
    /// </summary>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateUser(Guid id)
    {
        try
        {
            var user = await _repository.GetByIdAsync(id);
            user.Activate();
            await _repository.SaveAsync(user);

            _logger.LogInformation("User activated: {UserId}", id);

            var response = MapToResponse(user);
            return Ok(response);
        }
        catch (AggregateNotFoundException)
        {
            return NotFound(new { error = $"User with ID {id} not found" });
        }
    }

    /// <summary>
    /// Deactivate user
    /// </summary>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeactivateUser(Guid id, [FromBody] DeactivateUserRequest request)
    {
        try
        {
            var user = await _repository.GetByIdAsync(id);
            user.Deactivate(request.Reason);
            await _repository.SaveAsync(user);

            _logger.LogInformation("User deactivated: {UserId}, Reason: {Reason}", id, request.Reason);

            var response = MapToResponse(user);
            return Ok(response);
        }
        catch (AggregateNotFoundException)
        {
            return NotFound(new { error = $"User with ID {id} not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all events for a specific user (event history/audit trail)
    /// </summary>
    [HttpGet("{id}/events")]
    [ProducesResponseType(typeof(IEnumerable<EventEnvelope>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserEvents(Guid id)
    {
        // Check if user exists
        if (!await _repository.ExistsAsync(id))
        {
            return NotFound(new { error = $"User with ID {id} not found" });
        }

        var eventStore = HttpContext.RequestServices.GetRequiredService<IEventStore>();
        var events = await eventStore.GetEventEnvelopesAsync(id, "UserAggregate");

        _logger.LogInformation("Retrieved {Count} events for user {UserId}", events.Count(), id);

        return Ok(events);
    }

    private static UserResponse MapToResponse(UserAggregate user)
    {
        return new UserResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsActive,
            user.DeactivationReason,
            user.Version
        );
    }
}
