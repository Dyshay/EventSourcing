using EventSourcing.Core.StateMachine;
using FluentAssertions;
using Xunit;

namespace EventSourcing.Tests.Core;

public class StateMachineTests
{
    private enum TrafficLight
    {
        Red,
        Yellow,
        Green
    }

    [Fact]
    public void Constructor_ShouldSetInitialState()
    {
        // Arrange & Act
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red);

        // Assert
        stateMachine.CurrentState.Should().Be(TrafficLight.Red);
        stateMachine.PreviousState.Should().BeNull();
    }

    [Fact]
    public void Allow_ShouldDefineAllowedTransition()
    {
        // Arrange
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red);
        stateMachine.Allow(TrafficLight.Red, TrafficLight.Green);

        // Act
        var canTransition = stateMachine.CanTransitionTo(TrafficLight.Green);

        // Assert
        canTransition.Should().BeTrue();
    }

    [Fact]
    public void TransitionTo_WithAllowedTransition_ShouldChangeState()
    {
        // Arrange
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red);
        stateMachine.Allow(TrafficLight.Red, TrafficLight.Green);

        // Act
        stateMachine.TransitionTo(TrafficLight.Green);

        // Assert
        stateMachine.CurrentState.Should().Be(TrafficLight.Green);
        stateMachine.PreviousState.Should().Be(TrafficLight.Red);
    }

    [Fact]
    public void TransitionTo_WithDisallowedTransition_ShouldThrowException()
    {
        // Arrange
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red);
        stateMachine.Allow(TrafficLight.Red, TrafficLight.Green); // Only allow Red -> Green

        // Act
        var act = () => stateMachine.TransitionTo(TrafficLight.Yellow);

        // Assert
        act.Should().Throw<InvalidStateTransitionException>()
            .WithMessage("*Red*Yellow*not allowed*");
    }

    [Fact]
    public void TransitionTo_ToCurrentState_ShouldNotThrow()
    {
        // Arrange
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red);

        // Act
        var act = () => stateMachine.TransitionTo(TrafficLight.Red);

        // Assert
        act.Should().NotThrow();
        stateMachine.CurrentState.Should().Be(TrafficLight.Red);
    }

    [Fact]
    public void Allow_WithMultipleStates_ShouldAllowAllTransitions()
    {
        // Arrange
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red);
        stateMachine.Allow(TrafficLight.Red, TrafficLight.Green, TrafficLight.Yellow);

        // Act & Assert
        stateMachine.CanTransitionTo(TrafficLight.Green).Should().BeTrue();
        stateMachine.CanTransitionTo(TrafficLight.Yellow).Should().BeTrue();
    }

    [Fact]
    public void OnEnter_ShouldExecuteActionWhenEnteringState()
    {
        // Arrange
        var actionExecuted = false;
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red);
        stateMachine.Allow(TrafficLight.Red, TrafficLight.Green);
        stateMachine.OnEnter(TrafficLight.Green, () => actionExecuted = true);

        // Act
        stateMachine.TransitionTo(TrafficLight.Green);

        // Assert
        actionExecuted.Should().BeTrue();
    }

    [Fact]
    public void OnExit_ShouldExecuteActionWhenExitingState()
    {
        // Arrange
        var actionExecuted = false;
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red);
        stateMachine.Allow(TrafficLight.Red, TrafficLight.Green);
        stateMachine.OnExit(TrafficLight.Red, () => actionExecuted = true);

        // Act
        stateMachine.TransitionTo(TrafficLight.Green);

        // Assert
        actionExecuted.Should().BeTrue();
    }

    [Fact]
    public void GetAllowedTransitions_ShouldReturnAllowedStates()
    {
        // Arrange
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red);
        stateMachine.Allow(TrafficLight.Red, TrafficLight.Green, TrafficLight.Yellow);

        // Act
        var allowedTransitions = stateMachine.GetAllowedTransitions().ToList();

        // Assert
        allowedTransitions.Should().HaveCount(2);
        allowedTransitions.Should().Contain(TrafficLight.Green);
        allowedTransitions.Should().Contain(TrafficLight.Yellow);
    }

    [Fact]
    public void GetAllowedTransitions_FromStateWithNoTransitions_ShouldReturnEmpty()
    {
        // Arrange
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red);

        // Act
        var allowedTransitions = stateMachine.GetAllowedTransitions();

        // Assert
        allowedTransitions.Should().BeEmpty();
    }

    [Fact]
    public void SetState_ShouldChangeStateWithoutValidation()
    {
        // Arrange
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red);
        // No transitions defined - TransitionTo would throw

        // Act
        stateMachine.SetState(TrafficLight.Green);

        // Assert
        stateMachine.CurrentState.Should().Be(TrafficLight.Green);
        stateMachine.PreviousState.Should().Be(TrafficLight.Red);
    }

    [Fact]
    public void FluentConfiguration_ShouldAllowChainedCalls()
    {
        // Arrange & Act
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red)
            .Allow(TrafficLight.Red, TrafficLight.Green)
            .Allow(TrafficLight.Green, TrafficLight.Yellow)
            .Allow(TrafficLight.Yellow, TrafficLight.Red)
            .OnEnter(TrafficLight.Green, () => { })
            .OnExit(TrafficLight.Red, () => { });

        // Assert
        stateMachine.CanTransitionTo(TrafficLight.Green).Should().BeTrue();
    }

    [Fact]
    public void ComplexStateMachine_ShouldHandleMultipleTransitions()
    {
        // Arrange - Traffic light cycle
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red)
            .Allow(TrafficLight.Red, TrafficLight.Green)
            .Allow(TrafficLight.Green, TrafficLight.Yellow)
            .Allow(TrafficLight.Yellow, TrafficLight.Red);

        // Act & Assert - Full cycle
        stateMachine.TransitionTo(TrafficLight.Green);
        stateMachine.CurrentState.Should().Be(TrafficLight.Green);

        stateMachine.TransitionTo(TrafficLight.Yellow);
        stateMachine.CurrentState.Should().Be(TrafficLight.Yellow);

        stateMachine.TransitionTo(TrafficLight.Red);
        stateMachine.CurrentState.Should().Be(TrafficLight.Red);
    }

    [Fact]
    public void OnEnterAndOnExit_ShouldExecuteInCorrectOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        var stateMachine = new StateMachine<TrafficLight>(TrafficLight.Red)
            .Allow(TrafficLight.Red, TrafficLight.Green)
            .OnExit(TrafficLight.Red, () => executionOrder.Add("Exit Red"))
            .OnEnter(TrafficLight.Green, () => executionOrder.Add("Enter Green"));

        // Act
        stateMachine.TransitionTo(TrafficLight.Green);

        // Assert
        executionOrder.Should().Equal("Exit Red", "Enter Green");
    }
}
