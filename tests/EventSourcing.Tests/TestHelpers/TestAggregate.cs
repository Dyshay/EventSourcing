using EventSourcing.Core;

namespace EventSourcing.Tests.TestHelpers;

public class TestAggregate : AggregateBase<Guid>
{
    private Guid _id;

    public override Guid Id
    {
        get => _id;
        protected set => _id = value;
    }

    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public int Counter { get; private set; }

    // Methods for raising events
    public void Create(Guid id, string name, string email)
    {
        RaiseEvent(new TestAggregateCreatedEvent(id, name, email));
    }

    public void Rename(string newName)
    {
        RaiseEvent(new TestAggregateRenamedEvent(newName));
    }

    public void ChangeEmail(string newEmail)
    {
        RaiseEvent(new TestAggregateEmailChangedEvent(newEmail));
    }

    public void IncrementCounter()
    {
        RaiseEvent(new TestAggregateCounterIncrementedEvent());
    }

    // Apply methods for event replay
    private void Apply(TestAggregateCreatedEvent evt)
    {
        Id = evt.Id;
        Name = evt.Name;
        Email = evt.Email;
    }

    private void Apply(TestAggregateRenamedEvent evt)
    {
        Name = evt.NewName;
    }

    private void Apply(TestAggregateEmailChangedEvent evt)
    {
        Email = evt.NewEmail;
    }

    private void Apply(TestAggregateCounterIncrementedEvent evt)
    {
        Counter++;
    }
}
