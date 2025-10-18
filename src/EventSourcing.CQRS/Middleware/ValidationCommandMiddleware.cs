using EventSourcing.CQRS.Commands;
using EventSourcing.CQRS.Context;

namespace EventSourcing.CQRS.Middleware;

/// <summary>
/// Base interface for command validators
/// </summary>
public interface ICommandValidator<in TCommand> where TCommand : ICommand
{
    /// <summary>
    /// Validates the command and returns validation errors (empty if valid)
    /// </summary>
    Task<IEnumerable<string>> ValidateAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Middleware that validates commands before execution
/// </summary>
public class ValidationCommandMiddleware<TCommand> : ICommandMiddleware<TCommand>
    where TCommand : ICommand
{
    private readonly IEnumerable<ICommandValidator<TCommand>> _validators;

    public int Order => 20; // Execute before business logic

    public ValidationCommandMiddleware(IEnumerable<ICommandValidator<TCommand>> validators)
    {
        _validators = validators;
    }

    public async Task<TResult> InvokeAsync<TResult>(
        TCommand command,
        CommandContext context,
        CommandHandlerDelegate<TResult> next,
        CancellationToken cancellationToken)
    {
        // Run all validators
        var validationTasks = _validators
            .Select(v => v.ValidateAsync(command, cancellationToken));

        var validationResults = await Task.WhenAll(validationTasks);

        var errors = validationResults
            .SelectMany(e => e)
            .ToList();

        if (errors.Any())
        {
            var errorMessage = string.Join("; ", errors);
            throw new ValidationException($"Command validation failed: {errorMessage}", errors);
        }

        return await next();
    }
}

/// <summary>
/// Exception thrown when command validation fails
/// </summary>
public class ValidationException : Exception
{
    public IEnumerable<string> Errors { get; }

    public ValidationException(string message, IEnumerable<string> errors)
        : base(message)
    {
        Errors = errors;
    }
}
