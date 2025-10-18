using EventSourcing.Abstractions.Sagas;
using EventSourcing.Core.Sagas;
using FluentAssertions;
using Xunit;

namespace EventSourcing.Tests.Sagas;

public class SagaTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateSaga()
    {
        // Arrange
        var data = new TestSagaData { Id = "test-1" };

        // Act
        var saga = new Saga<TestSagaData>("TestSaga", data);

        // Assert
        saga.SagaName.Should().Be("TestSaga");
        saga.Data.Should().BeSameAs(data);
        saga.SagaId.Should().NotBeNullOrEmpty();
        saga.Status.Should().Be(SagaStatus.NotStarted);
        saga.CurrentStepIndex.Should().Be(-1);
        saga.Steps.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithCustomSagaId_ShouldUseThatId()
    {
        // Arrange
        var data = new TestSagaData();
        var customId = "custom-saga-id";

        // Act
        var saga = new Saga<TestSagaData>("TestSaga", data, customId);

        // Assert
        saga.SagaId.Should().Be(customId);
    }

    [Fact]
    public void Constructor_WithNullSagaName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var data = new TestSagaData();

        // Act
        Action act = () => new Saga<TestSagaData>(null!, data);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("sagaName");
    }

    [Fact]
    public void Constructor_WithNullData_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new Saga<TestSagaData>("TestSaga", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("data");
    }

    [Fact]
    public void AddStep_WithValidStep_ShouldAddToSteps()
    {
        // Arrange
        var saga = new Saga<TestSagaData>("TestSaga", new TestSagaData());
        var step = new SuccessfulStep("Step1");

        // Act
        var result = saga.AddStep(step);

        // Assert
        result.Should().BeSameAs(saga); // Fluent interface
        saga.Steps.Should().HaveCount(1);
        saga.Steps[0].Should().BeSameAs(step);
    }

    [Fact]
    public void AddStep_WithNullStep_ShouldThrowArgumentNullException()
    {
        // Arrange
        var saga = new Saga<TestSagaData>("TestSaga", new TestSagaData());

        // Act
        Action act = () => saga.AddStep(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("step");
    }

    [Fact]
    public void AddStep_WhenSagaAlreadyStarted_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var saga = new Saga<TestSagaData>("TestSaga", new TestSagaData());
        saga.RestoreState(SagaStatus.Running, 0);
        var step = new SuccessfulStep("Step1");

        // Act
        Action act = () => saga.AddStep(step);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot add steps to a saga that has already started");
    }

    [Fact]
    public void AddSteps_WithMultipleSteps_ShouldAddAllSteps()
    {
        // Arrange
        var saga = new Saga<TestSagaData>("TestSaga", new TestSagaData());
        var step1 = new SuccessfulStep("Step1");
        var step2 = new SuccessfulStep("Step2");
        var step3 = new SuccessfulStep("Step3");

        // Act
        var result = saga.AddSteps(step1, step2, step3);

        // Assert
        result.Should().BeSameAs(saga); // Fluent interface
        saga.Steps.Should().HaveCount(3);
        saga.Steps[0].Should().BeSameAs(step1);
        saga.Steps[1].Should().BeSameAs(step2);
        saga.Steps[2].Should().BeSameAs(step3);
    }

    [Fact]
    public void RestoreState_ShouldUpdateStatusAndStepIndex()
    {
        // Arrange
        var saga = new Saga<TestSagaData>("TestSaga", new TestSagaData());

        // Act
        saga.RestoreState(SagaStatus.Running, 2);

        // Assert
        saga.Status.Should().Be(SagaStatus.Running);
        saga.CurrentStepIndex.Should().Be(2);
    }

    [Fact]
    public void Saga_FluentAPI_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var saga = new Saga<TestSagaData>("TestSaga", new TestSagaData())
            .AddStep(new SuccessfulStep("Step1"))
            .AddStep(new SuccessfulStep("Step2"))
            .AddSteps(
                new SuccessfulStep("Step3"),
                new SuccessfulStep("Step4")
            );

        // Assert
        saga.Steps.Should().HaveCount(4);
        saga.Status.Should().Be(SagaStatus.NotStarted);
    }
}
