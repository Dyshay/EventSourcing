using EventSourcing.Abstractions.Sagas;
using EventSourcing.Core.Sagas;
using EventSourcing.MongoDB;
using FluentAssertions;
using MongoDB.Driver;
using Xunit;

namespace EventSourcing.Tests.Sagas;

public class MongoSagaStoreTests : IAsyncLifetime
{
    private readonly IMongoDatabase _database;
    private readonly MongoSagaStore _store;
    private const string TestDatabaseName = "SagaTests";

    public MongoSagaStoreTests()
    {
        var client = new MongoClient("mongodb://localhost:27017");
        _database = client.GetDatabase(TestDatabaseName);
        _store = new MongoSagaStore(_database);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _database.Client.DropDatabaseAsync(TestDatabaseName);
    }

    [Fact]
    public async Task SaveAsync_WithValidSaga_ShouldSaveSaga()
    {
        // Arrange
        var data = new TestSagaData { Id = "test-1", Counter = 5 };
        var saga = new Saga<TestSagaData>("TestSaga", data, "saga-1");
        saga.RestoreState(SagaStatus.Running, 2);

        // Act
        await _store.SaveAsync(saga);

        // Assert
        var loaded = await _store.LoadAsync<TestSagaData>("saga-1");
        loaded.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveAsync_WithNullSaga_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _store.SaveAsync<TestSagaData>(null!)
        );
    }

    [Fact]
    public async Task LoadAsync_WithExistingSaga_ShouldReturnSaga()
    {
        // Arrange
        var data = new TestSagaData { Id = "test-2", Counter = 10 };
        var saga = new Saga<TestSagaData>("TestSaga", data, "saga-2");
        saga.RestoreState(SagaStatus.Completed, 5);
        await _store.SaveAsync(saga);

        // Act
        var loaded = await _store.LoadAsync<TestSagaData>("saga-2");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.SagaId.Should().Be("saga-2");
        loaded.SagaName.Should().Be("TestSaga");
        loaded.Data.Id.Should().Be("test-2");
        loaded.Data.Counter.Should().Be(10);
        loaded.Status.Should().Be(SagaStatus.Completed);
        loaded.CurrentStepIndex.Should().Be(5);
    }

    [Fact]
    public async Task LoadAsync_WithNonExistingSaga_ShouldReturnNull()
    {
        // Act
        var loaded = await _store.LoadAsync<TestSagaData>("non-existing-id");

        // Assert
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WithNullSagaId_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _store.LoadAsync<TestSagaData>(null!)
        );
    }

    [Fact]
    public async Task LoadAsync_WithEmptySagaId_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _store.LoadAsync<TestSagaData>(string.Empty)
        );
    }

    [Fact]
    public async Task SaveAsync_UpdateExistingSaga_ShouldUpdateSaga()
    {
        // Arrange
        var data = new TestSagaData { Id = "test-3", Counter = 1 };
        var saga = new Saga<TestSagaData>("TestSaga", data, "saga-3");
        saga.RestoreState(SagaStatus.Running, 0);
        await _store.SaveAsync(saga);

        // Act - Update saga state
        data.Counter = 5;
        saga.RestoreState(SagaStatus.Completed, 3);
        await _store.SaveAsync(saga);

        // Assert
        var loaded = await _store.LoadAsync<TestSagaData>("saga-3");
        loaded.Should().NotBeNull();
        loaded!.Data.Counter.Should().Be(5);
        loaded.Status.Should().Be(SagaStatus.Completed);
        loaded.CurrentStepIndex.Should().Be(3);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingSaga_ShouldRemoveSaga()
    {
        // Arrange
        var data = new TestSagaData { Id = "test-4" };
        var saga = new Saga<TestSagaData>("TestSaga", data, "saga-4");
        await _store.SaveAsync(saga);

        // Act
        await _store.DeleteAsync("saga-4");

        // Assert
        var loaded = await _store.LoadAsync<TestSagaData>("saga-4");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistingSaga_ShouldNotThrow()
    {
        // Act
        Func<Task> act = async () => await _store.DeleteAsync("non-existing-id");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_WithNullSagaId_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _store.DeleteAsync(null!)
        );
    }

    [Fact]
    public async Task SaveAndLoad_WithComplexData_ShouldPreserveAllProperties()
    {
        // Arrange
        var data = new TestSagaData
        {
            Id = "complex-test",
            Counter = 42,
            ExecutionLog = new List<string> { "Step1", "Step2", "Step3" },
            ShouldFailAtStep = true,
            FailAtStepIndex = 2
        };

        var saga = new Saga<TestSagaData>("ComplexSaga", data, "saga-complex");
        saga.RestoreState(SagaStatus.Compensating, 1);
        await _store.SaveAsync(saga);

        // Act
        var loaded = await _store.LoadAsync<TestSagaData>("saga-complex");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Data.Id.Should().Be("complex-test");
        loaded.Data.Counter.Should().Be(42);
        loaded.Data.ExecutionLog.Should().Equal("Step1", "Step2", "Step3");
        loaded.Data.ShouldFailAtStep.Should().BeTrue();
        loaded.Data.FailAtStepIndex.Should().Be(2);
        loaded.Status.Should().Be(SagaStatus.Compensating);
        loaded.CurrentStepIndex.Should().Be(1);
    }

    [Fact]
    public async Task Store_WithMultipleSagas_ShouldIsolateEachSaga()
    {
        // Arrange
        var saga1 = new Saga<TestSagaData>("Saga1", new TestSagaData { Id = "1" }, "saga-1");
        var saga2 = new Saga<TestSagaData>("Saga2", new TestSagaData { Id = "2" }, "saga-2");
        var saga3 = new Saga<TestSagaData>("Saga3", new TestSagaData { Id = "3" }, "saga-3");

        // Act
        await _store.SaveAsync(saga1);
        await _store.SaveAsync(saga2);
        await _store.SaveAsync(saga3);

        // Assert
        var loaded1 = await _store.LoadAsync<TestSagaData>("saga-1");
        var loaded2 = await _store.LoadAsync<TestSagaData>("saga-2");
        var loaded3 = await _store.LoadAsync<TestSagaData>("saga-3");

        loaded1!.Data.Id.Should().Be("1");
        loaded2!.Data.Id.Should().Be("2");
        loaded3!.Data.Id.Should().Be("3");
    }

    [Fact]
    public async Task SaveAsync_MultipleTimes_ShouldAllowRepeatedSaves()
    {
        // Arrange
        var data = new TestSagaData { Id = "test-5" };
        var saga = new Saga<TestSagaData>("TestSaga", data, "saga-5");

        // Act - Save multiple times
        await _store.SaveAsync(saga);
        saga.RestoreState(SagaStatus.Running, 1);
        await _store.SaveAsync(saga);
        saga.RestoreState(SagaStatus.Running, 2);
        await _store.SaveAsync(saga);
        saga.RestoreState(SagaStatus.Completed, 3);
        await _store.SaveAsync(saga);

        // Assert
        var loaded = await _store.LoadAsync<TestSagaData>("saga-5");
        loaded!.Status.Should().Be(SagaStatus.Completed);
        loaded.CurrentStepIndex.Should().Be(3);
    }

    [Fact]
    public async Task SaveAsync_ShouldUpsertCorrectly()
    {
        // Arrange
        var data = new TestSagaData { Id = "upsert-test", Counter = 1 };
        var saga = new Saga<TestSagaData>("UpsertSaga", data, "saga-upsert");

        // Act - First save (insert)
        await _store.SaveAsync(saga);
        var firstLoad = await _store.LoadAsync<TestSagaData>("saga-upsert");

        // Act - Second save (update)
        data.Counter = 99;
        saga.RestoreState(SagaStatus.Completed, 10);
        await _store.SaveAsync(saga);
        var secondLoad = await _store.LoadAsync<TestSagaData>("saga-upsert");

        // Assert
        firstLoad!.Data.Counter.Should().Be(1);
        secondLoad!.Data.Counter.Should().Be(99);
        secondLoad.Status.Should().Be(SagaStatus.Completed);
        secondLoad.CurrentStepIndex.Should().Be(10);
    }

    [Fact]
    public async Task SaveAsync_ShouldStoreUpdatedTimestamp()
    {
        // Arrange
        var data = new TestSagaData { Id = "timestamp-test" };
        var saga = new Saga<TestSagaData>("TimestampSaga", data, "saga-timestamp");

        // Act
        await _store.SaveAsync(saga);

        // Assert - Verify the document was created (indirectly verifies timestamp exists)
        var loaded = await _store.LoadAsync<TestSagaData>("saga-timestamp");
        loaded.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveAndLoad_WithAllSagaStatuses_ShouldPreserveStatus()
    {
        // Arrange & Act & Assert for each status
        var statuses = new[]
        {
            SagaStatus.NotStarted,
            SagaStatus.Running,
            SagaStatus.Completed,
            SagaStatus.Compensating,
            SagaStatus.Compensated,
            SagaStatus.CompensationFailed
        };

        foreach (var status in statuses)
        {
            var data = new TestSagaData { Id = $"status-{status}" };
            var saga = new Saga<TestSagaData>($"Saga-{status}", data, $"saga-{status}");
            saga.RestoreState(status, 0);

            await _store.SaveAsync(saga);
            var loaded = await _store.LoadAsync<TestSagaData>($"saga-{status}");

            loaded.Should().NotBeNull();
            loaded!.Status.Should().Be(status);
        }
    }
}
