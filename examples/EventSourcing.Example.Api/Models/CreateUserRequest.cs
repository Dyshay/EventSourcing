namespace EventSourcing.Example.Api.Models;

public record CreateUserRequest(
    string Email,
    string FirstName,
    string LastName
);
