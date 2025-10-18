using Microsoft.Extensions.ObjectPool;

namespace EventSourcing.CQRS.Context;

/// <summary>
/// Pooling policy for CommandContext objects
/// </summary>
public class CommandContextPoolPolicy : PooledObjectPolicy<CommandContext>
{
    public override CommandContext Create()
    {
        return new CommandContext();
    }

    public override bool Return(CommandContext obj)
    {
        // Reset the context before returning to pool
        obj.Reset();
        return true;
    }
}
