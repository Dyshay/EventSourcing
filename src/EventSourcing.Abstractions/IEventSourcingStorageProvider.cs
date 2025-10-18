namespace EventSourcing.Abstractions;

/// <summary>
/// Abstraction for event sourcing storage providers.
/// Implement this interface to create support for different databases (PostgreSQL, SQL Server, CosmosDB, etc.)
/// </summary>
public interface IEventSourcingStorageProvider
{
    /// <summary>
    /// Creates and configures an event store implementation.
    /// </summary>
    /// <returns>Configured event store instance</returns>
    IEventStore CreateEventStore();

    /// <summary>
    /// Creates and configures a snapshot store implementation.
    /// </summary>
    /// <returns>Configured snapshot store instance</returns>
    ISnapshotStore CreateSnapshotStore();

    /// <summary>
    /// Initializes the storage (creates tables, collections, indexes, etc.).
    /// Called during application startup.
    /// </summary>
    /// <param name="aggregateTypes">Types of aggregates to initialize storage for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task InitializeAsync(IEnumerable<string> aggregateTypes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the storage configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    void ValidateConfiguration();
}
