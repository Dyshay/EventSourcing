namespace EventSourcing.Tests.Sagas;

/// <summary>
/// Test saga data for unit tests
/// </summary>
public class TestSagaData
{
    public string Id { get; set; } = string.Empty;
    public int Counter { get; set; }
    public List<string> ExecutionLog { get; set; } = new();
    public bool ShouldFailAtStep { get; set; }
    public int FailAtStepIndex { get; set; }
}
