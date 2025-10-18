using EventSourcing.Core.Projections;

namespace EventSourcing.Tests.TestHelpers;

public class TestProjection : IProjection
{
    public List<TestAggregateCreatedEvent> CreatedEvents { get; } = new();
    public List<TestAggregateRenamedEvent> RenamedEvents { get; } = new();

    public Task Handle(TestAggregateCreatedEvent evt, CancellationToken cancellationToken = default)
    {
        CreatedEvents.Add(evt);
        return Task.CompletedTask;
    }

    public Task Handle(TestAggregateRenamedEvent evt)
    {
        RenamedEvents.Add(evt);
        return Task.CompletedTask;
    }
}
