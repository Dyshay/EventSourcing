using Xunit;

namespace EventSourcing.Tests.MongoDB;

/// <summary>
/// Defines a test collection for MongoDB tests to ensure they run sequentially.
/// All MongoDB tests share the same "test" database, so parallel execution causes interference.
/// </summary>
[CollectionDefinition("MongoDB Collection")]
public class MongoTestCollection
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
