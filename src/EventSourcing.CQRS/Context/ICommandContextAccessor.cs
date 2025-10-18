namespace EventSourcing.CQRS.Context;

/// <summary>
/// Provides access to the current command context in the execution pipeline.
/// Similar to IHttpContextAccessor in ASP.NET Core.
/// </summary>
public interface ICommandContextAccessor
{
    /// <summary>
    /// Gets or sets the current command context
    /// </summary>
    CommandContext? CurrentContext { get; set; }
}

/// <summary>
/// Default implementation using AsyncLocal for async-safe storage
/// </summary>
public class CommandContextAccessor : ICommandContextAccessor
{
    private static readonly AsyncLocal<CommandContext?> _currentContext = new();

    public CommandContext? CurrentContext
    {
        get => _currentContext.Value;
        set => _currentContext.Value = value;
    }
}
