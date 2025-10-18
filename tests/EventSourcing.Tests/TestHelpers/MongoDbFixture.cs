namespace EventSourcing.Tests.TestHelpers;

/// <summary>
/// Helper class for MongoDB connection configuration in tests
/// </summary>
public static class MongoDbFixture
{
    /// <summary>
    /// Gets the MongoDB connection string from environment variable or uses default localhost
    /// </summary>
    public static string GetConnectionString()
    {
        // Try to get connection string from environment variable first
        var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");

        // Fall back to localhost if not set
        return string.IsNullOrEmpty(connectionString)
            ? "mongodb://localhost:27017"
            : connectionString;
    }

    /// <summary>
    /// Gets the timeout for MongoDB operations (shorter in CI environments)
    /// </summary>
    public static TimeSpan GetConnectionTimeout()
    {
        var isCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));
        return isCI ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(5);
    }
}
