namespace EventSourcing.Example.Api.Models;

public record UpdateUserNameRequest(
    string FirstName,
    string LastName
);
