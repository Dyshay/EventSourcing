using EventSourcing.Core.Sagas;

namespace EventSourcing.Tests.Sagas;

/// <summary>
/// Test saga step that always succeeds
/// </summary>
public class SuccessfulStep : SagaStepBase<TestSagaData>
{
    private readonly string _stepName;

    public SuccessfulStep(string stepName)
    {
        _stepName = stepName;
    }

    public override string Name => _stepName;

    public override Task<bool> ExecuteAsync(TestSagaData data, CancellationToken cancellationToken = default)
    {
        data.Counter++;
        data.ExecutionLog.Add($"Execute:{_stepName}");
        return Task.FromResult(true);
    }

    public override Task<bool> CompensateAsync(TestSagaData data, CancellationToken cancellationToken = default)
    {
        data.Counter--;
        data.ExecutionLog.Add($"Compensate:{_stepName}");
        return Task.FromResult(true);
    }
}

/// <summary>
/// Test saga step that fails conditionally
/// </summary>
public class ConditionalFailureStep : SagaStepBase<TestSagaData>
{
    private readonly string _stepName;
    private readonly int _stepIndex;

    public ConditionalFailureStep(string stepName, int stepIndex)
    {
        _stepName = stepName;
        _stepIndex = stepIndex;
    }

    public override string Name => _stepName;

    public override Task<bool> ExecuteAsync(TestSagaData data, CancellationToken cancellationToken = default)
    {
        data.ExecutionLog.Add($"Execute:{_stepName}");

        if (data.ShouldFailAtStep && data.FailAtStepIndex == _stepIndex)
        {
            data.ExecutionLog.Add($"Failed:{_stepName}");
            return Task.FromResult(false);
        }

        data.Counter++;
        return Task.FromResult(true);
    }

    public override Task<bool> CompensateAsync(TestSagaData data, CancellationToken cancellationToken = default)
    {
        data.Counter--;
        data.ExecutionLog.Add($"Compensate:{_stepName}");
        return Task.FromResult(true);
    }
}

/// <summary>
/// Test saga step that throws an exception
/// </summary>
public class ExceptionThrowingStep : SagaStepBase<TestSagaData>
{
    public override string Name => "ExceptionThrowingStep";

    public override Task<bool> ExecuteAsync(TestSagaData data, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Step execution failed");
    }

    public override Task<bool> CompensateAsync(TestSagaData data, CancellationToken cancellationToken = default)
    {
        data.ExecutionLog.Add("Compensate:ExceptionThrowingStep");
        return Task.FromResult(true);
    }
}

/// <summary>
/// Test saga step with compensation failure
/// </summary>
public class CompensationFailureStep : SagaStepBase<TestSagaData>
{
    public override string Name => "CompensationFailureStep";

    public override Task<bool> ExecuteAsync(TestSagaData data, CancellationToken cancellationToken = default)
    {
        data.Counter++;
        data.ExecutionLog.Add("Execute:CompensationFailureStep");
        return Task.FromResult(true);
    }

    public override Task<bool> CompensateAsync(TestSagaData data, CancellationToken cancellationToken = default)
    {
        data.ExecutionLog.Add("Compensate:CompensationFailureStep");
        return Task.FromResult(false); // Compensation fails
    }
}
