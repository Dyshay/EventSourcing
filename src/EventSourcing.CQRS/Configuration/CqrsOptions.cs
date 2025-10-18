namespace EventSourcing.CQRS.Configuration;

/// <summary>
/// Configuration options for CQRS framework
/// </summary>
public class CqrsOptions
{
    /// <summary>
    /// Enable or disable audit trail tracking for commands.
    /// When disabled, CommandContext will not be created, improving performance.
    /// Default: true (enabled for backward compatibility)
    /// </summary>
    public bool EnableAuditTrail { get; set; } = true;

    /// <summary>
    /// Enable or disable logging in CommandBus and QueryBus.
    /// Default: true
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Enable or disable query caching.
    /// Default: true
    /// </summary>
    public bool EnableQueryCache { get; set; } = true;

    /// <summary>
    /// Creates options with audit trail disabled for maximum performance
    /// </summary>
    public static CqrsOptions HighPerformance() => new()
    {
        EnableAuditTrail = false,
        EnableLogging = false,
        EnableQueryCache = true
    };

    /// <summary>
    /// Creates options with all features enabled (default)
    /// </summary>
    public static CqrsOptions Default() => new();

    /// <summary>
    /// Creates options with custom settings
    /// </summary>
    public static CqrsOptions Custom(
        bool enableAuditTrail = true,
        bool enableLogging = true,
        bool enableQueryCache = true) => new()
    {
        EnableAuditTrail = enableAuditTrail,
        EnableLogging = enableLogging,
        EnableQueryCache = enableQueryCache
    };
}
