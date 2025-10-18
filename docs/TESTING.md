# Testing Guide

## Running Tests

### Local Development

Run all tests locally:
```bash
dotnet test
```

Run only unit tests (skip MongoDB integration tests):
```bash
dotnet test --filter "Category!=Integration"
```

### Integration Tests with MongoDB

The test suite includes integration tests that require MongoDB. By default, tests connect to `mongodb://localhost:27017`.

#### Prerequisites

1. **Install MongoDB** (for local development)
   ```bash
   # Windows (with Chocolatey)
   choco install mongodb

   # macOS (with Homebrew)
   brew install mongodb-community

   # Linux (Ubuntu)
   sudo apt-get install mongodb
   ```

2. **Start MongoDB**
   ```bash
   # Windows
   mongod

   # macOS/Linux
   brew services start mongodb-community
   # or
   sudo systemctl start mongod
   ```

#### Using a Remote MongoDB Instance

You can configure tests to use a remote MongoDB instance using an environment variable:

**Windows (PowerShell):**
```powershell
$env:MONGODB_CONNECTION_STRING="mongodb://username:password@your-remote-server:27017"
dotnet test
```

**Windows (CMD):**
```cmd
set MONGODB_CONNECTION_STRING=mongodb://username:password@your-remote-server:27017
dotnet test
```

**macOS/Linux:**
```bash
export MONGODB_CONNECTION_STRING="mongodb://username:password@your-remote-server:27017"
dotnet test
```

**Using MongoDB Atlas:**
```bash
export MONGODB_CONNECTION_STRING="mongodb+srv://username:password@cluster.mongodb.net/"
dotnet test
```

### Test Behavior

| Scenario | Behavior |
|----------|----------|
| MongoDB available | ✅ All 170 tests run |
| MongoDB unavailable | ⚠️ Integration tests skipped (158 tests run) |
| Connection timeout | Tests fail fast (5-10s) instead of hanging |

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `MONGODB_CONNECTION_STRING` | MongoDB connection string | `mongodb://localhost:27017` |
| `CI` | CI environment indicator (enables longer timeouts) | `false` |

## Continuous Integration

GitHub Actions workflows automatically configure MongoDB:

- **CI Workflow** (`.github/workflows/ci.yml`): Uses MongoDB service container
- **Code Coverage** (`.github/workflows/code-coverage.yml`): Uses MongoDB service container

Both workflows set `MONGODB_CONNECTION_STRING=mongodb://localhost:27017` and `CI=true`.

## Test Categories

Tests are organized using xUnit traits:

```csharp
[Trait("Category", "Integration")]
public class MongoSagaStoreTests
```

**Filter examples:**
```bash
# Run only integration tests
dotnet test --filter "Category=Integration"

# Exclude integration tests
dotnet test --filter "Category!=Integration"

# Run specific test class
dotnet test --filter "FullyQualifiedName~SagaTests"
```

## Writing New Tests

### Unit Tests

Use in-memory implementations for fast, isolated tests:

```csharp
public class MyServiceTests
{
    private readonly InMemorySagaStore _store;

    public MyServiceTests()
    {
        _store = new InMemorySagaStore();
    }

    [Fact]
    public async Task MyTest()
    {
        // Arrange
        var saga = new Saga<TestData>("MySaga", new TestData());

        // Act
        await _store.SaveAsync(saga);

        // Assert
        var loaded = await _store.LoadAsync<TestData>(saga.SagaId);
        loaded.Should().NotBeNull();
    }
}
```

### Integration Tests

Mark with `[SkippableFact]` and `[Trait("Category", "Integration")]`:

```csharp
[Trait("Category", "Integration")]
public class MyIntegrationTests : IAsyncLifetime
{
    private readonly IMongoDatabase _database;

    public MyIntegrationTests()
    {
        var connectionString = TestHelpers.MongoDbFixture.GetConnectionString();
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase("MyTestDb");
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1));
        }
        catch (Exception)
        {
            throw new SkipException("MongoDB is not available");
        }
    }

    public async Task DisposeAsync()
    {
        await _database.Client.DropDatabaseAsync("MyTestDb");
    }

    [SkippableFact]
    public async Task MyMongoTest()
    {
        // Test implementation
    }
}
```

## Troubleshooting

### "MongoDB is not available" - Tests Skipped

**Cause:** MongoDB is not running or not accessible.

**Solution:**
1. Start MongoDB locally: `mongod`
2. Or set `MONGODB_CONNECTION_STRING` to a remote instance
3. Or run without integration tests: `dotnet test --filter "Category!=Integration"`

### Connection Timeout Errors

**Cause:** MongoDB is slow to respond or network issues.

**Solution:**
1. Check MongoDB is running: `mongosh --eval "db.adminCommand({ping: 1})"`
2. Verify connection string is correct
3. Check firewall/network settings for remote connections

### Tests Passing Locally but Failing in CI

**Cause:** Environment differences or missing configuration.

**Solution:**
1. Verify GitHub Actions has MongoDB service configured
2. Check environment variables are set in workflow
3. Review workflow logs for MongoDB startup errors

## Best Practices

1. **Fast Tests**: Use in-memory stores for unit tests
2. **Isolated Tests**: Each integration test should use a unique database name
3. **Cleanup**: Always implement `IAsyncLifetime.DisposeAsync()` to cleanup test data
4. **Skip Gracefully**: Use `SkipException` when dependencies are unavailable
5. **Descriptive Names**: Test method names should describe the scenario and expected outcome

## Coverage Reports

Generate coverage reports locally:

```bash
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Install report generator (once)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator -reports:./coverage/**/coverage.cobertura.xml -targetdir:./coverage/report -reporttypes:Html

# Open report
start ./coverage/report/index.html  # Windows
open ./coverage/report/index.html   # macOS
xdg-open ./coverage/report/index.html  # Linux
```

## Test Data Management

Test databases are automatically created and destroyed:

- **Event Store Tests**: `EventSourcingTests` database
- **Snapshot Tests**: `EventSourcingSnapshotTests` database
- **Saga Tests**: `SagaTests` database

Each test class uses `IAsyncLifetime` to ensure proper cleanup.
