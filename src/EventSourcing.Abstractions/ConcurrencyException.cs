namespace EventSourcing.Abstractions;

/// <summary>
/// Exception thrown when an optimistic concurrency conflict is detected.
/// This occurs when the expected version doesn't match the actual version in the store.
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ConcurrencyException(object aggregateId, int expectedVersion, int actualVersion)
        : base($"Concurrency conflict for aggregate '{aggregateId}'. Expected version: {expectedVersion}, Actual version: {actualVersion}")
    {
        AggregateId = aggregateId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    public object? AggregateId { get; }
    public int? ExpectedVersion { get; }
    public int? ActualVersion { get; }
}
