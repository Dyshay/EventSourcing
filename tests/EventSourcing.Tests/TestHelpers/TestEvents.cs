using EventSourcing.Core;

namespace EventSourcing.Tests.TestHelpers;

public class TestAggregateCreatedEvent : DomainEvent
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Email { get; init; }

    public TestAggregateCreatedEvent(Guid id, string name, string email)
    {
        Id = id;
        Name = name;
        Email = email;
    }
}

public class TestAggregateRenamedEvent : DomainEvent
{
    public string NewName { get; init; }

    public TestAggregateRenamedEvent(string newName)
    {
        NewName = newName;
    }
}

public class TestAggregateEmailChangedEvent : DomainEvent
{
    public string NewEmail { get; init; }

    public TestAggregateEmailChangedEvent(string newEmail)
    {
        NewEmail = newEmail;
    }
}

public class TestAggregateCounterIncrementedEvent : DomainEvent
{
}
