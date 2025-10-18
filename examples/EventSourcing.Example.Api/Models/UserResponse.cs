namespace EventSourcing.Example.Api.Models;

public record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    string? DeactivationReason,
    int Version
);
