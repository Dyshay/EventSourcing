using EventSourcing.Abstractions.Sagas;
using EventSourcing.Core.Sagas;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventSourcing.Tests.Sagas;

public class SagaOrchestratorTests
{
    private readonly ISagaStore _sagaStore;
    private readonly ILogger<SagaOrchestrator> _logger;
    private readonly SagaOrchestrator _orchestrator;

    public SagaOrchestratorTests()
    {
        _sagaStore = new InMemorySagaStore();
        _logger = NullLogger<SagaOrchestrator>.Instance;
        _orchestrator = new SagaOrchestrator(_sagaStore, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_WithAllSuccessfulSteps_ShouldCompleteSuccessfully()
    {
        // Arrange
        var data = new TestSagaData { Id = "test-1" };
        var saga = new Saga<TestSagaData>("TestSaga", data)
            .AddSteps(
                new SuccessfulStep("Step1"),
                new SuccessfulStep("Step2"),
                new SuccessfulStep("Step3")
            );

        // Act
        var result = await _orchestrator.ExecuteAsync(saga);

        // Assert
        result.Status.Should().Be(SagaStatus.Completed);
        result.CurrentStepIndex.Should().Be(2); // Last step index
        data.Counter.Should().Be(3); // All 3 steps executed
        data.ExecutionLog.Should().Equal("Execute:Step1", "Execute:Step2", "Execute:Step3");
    }

    [Fact]
    public async Task ExecuteAsync_WithFailingSecondStep_ShouldCompensateFirstStep()
    {
        // Arrange
        var data = new TestSagaData
        {
            Id = "test-2",
            ShouldFailAtStep = true,
            FailAtStepIndex = 1 // Fail at second step
        };

        var saga = new Saga<TestSagaData>("TestSaga", data)
            .AddSteps(
                new ConditionalFailureStep("Step1", 0),
                new ConditionalFailureStep("Step2", 1), // This will fail
                new ConditionalFailureStep("Step3", 2)
            );

        // Act
        var result = await _orchestrator.ExecuteAsync(saga);

        // Assert
        result.Status.Should().Be(SagaStatus.Compensated);
        data.Counter.Should().Be(0); // Step1 executed, then compensated
        data.ExecutionLog.Should().Equal(
            "Execute:Step1",
            "Execute:Step2",
            "Failed:Step2",
            "Compensate:Step1"
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithFailingThirdStep_ShouldCompensateAllPreviousSteps()
    {
        // Arrange
        var data = new TestSagaData
        {
            Id = "test-3",
            ShouldFailAtStep = true,
            FailAtStepIndex = 2 // Fail at third step
        };

        var saga = new Saga<TestSagaData>("TestSaga", data)
            .AddSteps(
                new ConditionalFailureStep("Step1", 0),
                new ConditionalFailureStep("Step2", 1),
                new ConditionalFailureStep("Step3", 2) // This will fail
            );

        // Act
        var result = await _orchestrator.ExecuteAsync(saga);

        // Assert
        result.Status.Should().Be(SagaStatus.Compensated);
        data.Counter.Should().Be(0); // Step1 and Step2 executed, then both compensated
        data.ExecutionLog.Should().Equal(
            "Execute:Step1",
            "Execute:Step2",
            "Execute:Step3",
            "Failed:Step3",
            "Compensate:Step2",
            "Compensate:Step1"
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithExceptionInStep_ShouldCompensate()
    {
        // Arrange
        var data = new TestSagaData { Id = "test-4" };
        var saga = new Saga<TestSagaData>("TestSaga", data)
            .AddSteps(
                new SuccessfulStep("Step1"),
                new ExceptionThrowingStep(),
                new SuccessfulStep("Step3")
            );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orchestrator.ExecuteAsync(saga)
        );

        // Verify compensation occurred
        data.Counter.Should().Be(0); // Step1 executed, then compensated
        data.ExecutionLog.Should().Contain("Execute:Step1");
        data.ExecutionLog.Should().Contain("Compensate:Step1");
    }

    [Fact]
    public async Task ExecuteAsync_WithCompensationFailure_ShouldSetCompensationFailedStatus()
    {
        // Arrange
        var data = new TestSagaData
        {
            Id = "test-5",
            ShouldFailAtStep = true,
            FailAtStepIndex = 1
        };

        var saga = new Saga<TestSagaData>("TestSaga", data)
            .AddSteps(
                new CompensationFailureStep(), // This will fail during compensation
                new ConditionalFailureStep("Step2", 1) // This will fail
            );

        // Act
        var result = await _orchestrator.ExecuteAsync(saga);

        // Assert
        result.Status.Should().Be(SagaStatus.CompensationFailed);
        data.ExecutionLog.Should().Contain("Execute:CompensationFailureStep");
        data.ExecutionLog.Should().Contain("Execute:Step2");
        data.ExecutionLog.Should().Contain("Compensate:CompensationFailureStep");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSaveStateAtEachStep()
    {
        // Arrange
        var data = new TestSagaData { Id = "test-6" };
        var saga = new Saga<TestSagaData>("TestSaga", data)
            .AddSteps(
                new SuccessfulStep("Step1"),
                new SuccessfulStep("Step2")
            );

        // Act
        await _orchestrator.ExecuteAsync(saga);

        // Assert - Verify final state was saved
        var savedSaga = await _sagaStore.LoadAsync<TestSagaData>(saga.SagaId);
        savedSaga.Should().NotBeNull();
        savedSaga!.Status.Should().Be(SagaStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptySteps_ShouldCompleteImmediately()
    {
        // Arrange
        var data = new TestSagaData { Id = "test-7" };
        var saga = new Saga<TestSagaData>("TestSaga", data);

        // Act
        var result = await _orchestrator.ExecuteAsync(saga);

        // Assert
        result.Status.Should().Be(SagaStatus.Completed);
        data.Counter.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullSaga_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _orchestrator.ExecuteAsync<TestSagaData>(null!)
        );
    }

    [Fact]
    public async Task GetSagaAsync_WithExistingSaga_ShouldReturnSaga()
    {
        // Arrange
        var data = new TestSagaData { Id = "test-8" };
        var saga = new Saga<TestSagaData>("TestSaga", data);
        await _orchestrator.ExecuteAsync(saga);

        // Act
        var retrievedSaga = await _orchestrator.GetSagaAsync<TestSagaData>(saga.SagaId);

        // Assert
        retrievedSaga.Should().NotBeNull();
        retrievedSaga!.SagaId.Should().Be(saga.SagaId);
        retrievedSaga.Status.Should().Be(SagaStatus.Completed);
    }

    [Fact]
    public async Task GetSagaAsync_WithNonExistingSaga_ShouldReturnNull()
    {
        // Act
        var saga = await _orchestrator.GetSagaAsync<TestSagaData>("non-existing-id");

        // Assert
        saga.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUpdateCurrentStepIndexDuringExecution()
    {
        // Arrange
        var data = new TestSagaData { Id = "test-9" };
        var saga = new Saga<TestSagaData>("TestSaga", data)
            .AddSteps(
                new SuccessfulStep("Step1"),
                new SuccessfulStep("Step2"),
                new SuccessfulStep("Step3")
            );

        // Act
        var result = await _orchestrator.ExecuteAsync(saga);

        // Assert
        result.CurrentStepIndex.Should().Be(2); // Last step (0-indexed)
    }

    [Fact]
    public async Task ExecuteAsync_StateTransitions_ShouldBeCorrect()
    {
        // Arrange
        var data = new TestSagaData { Id = "test-10" };
        var saga = new Saga<TestSagaData>("TestSaga", data)
            .AddSteps(
                new SuccessfulStep("Step1"),
                new SuccessfulStep("Step2")
            );

        // Act
        var result = await _orchestrator.ExecuteAsync(saga);

        // Assert - Verify proper state transitions
        result.Status.Should().Be(SagaStatus.Completed);
        result.CurrentStepIndex.Should().Be(1); // Last step index (0-based)

        // Verify saga was saved in store
        var savedSaga = await _sagaStore.LoadAsync<TestSagaData>(saga.SagaId);
        savedSaga.Should().NotBeNull();
        savedSaga!.Status.Should().Be(SagaStatus.Completed);
    }
}
