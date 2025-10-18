using System.Text;
using System.Text.RegularExpressions;
using EventSourcing.Abstractions;

namespace EventSourcing.Core;

/// <summary>
/// Base record for domain events providing common event properties.
/// Inherit from this record to create your domain events.
/// </summary>
public abstract record DomainEvent : IEvent
{
    protected DomainEvent()
    {
        EventId = Guid.NewGuid();
        Timestamp = DateTimeOffset.UtcNow;
        EventType = GetType().Name;
        Kind = GenerateKindFromTypeName(GetType().Name);
    }

    /// <summary>
    /// Constructor that allows overriding the auto-generated Kind.
    /// </summary>
    /// <param name="kind">Custom kind/category for the event</param>
    protected DomainEvent(string kind)
    {
        EventId = Guid.NewGuid();
        Timestamp = DateTimeOffset.UtcNow;
        EventType = GetType().Name;
        Kind = kind;
    }

    public Guid EventId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string EventType { get; init; }
    public string Kind { get; init; }

    /// <summary>
    /// Generates a kind from the event type name.
    /// Example: "UserCreatedEvent" → "user.created"
    /// </summary>
    private static string GenerateKindFromTypeName(string typeName)
    {
        // Remove "Event" suffix if present
        if (typeName.EndsWith("Event", StringComparison.Ordinal))
        {
            typeName = typeName.Substring(0, typeName.Length - 5);
        }

        // Split on capital letters: "UserCreated" → ["User", "Created"]
        var words = Regex.Split(typeName, @"(?<!^)(?=[A-Z])");

        // Convert to lowercase and join with dots
        // First word is the aggregate, rest is the action
        if (words.Length == 1)
        {
            return words[0].ToLowerInvariant();
        }

        var aggregate = words[0].ToLowerInvariant();
        var action = string.Join("", words.Skip(1)).ToLowerInvariant();

        return $"{aggregate}.{action}";
    }
}
