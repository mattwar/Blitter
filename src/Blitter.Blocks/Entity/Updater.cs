namespace Blitter.Blocks;

/// <summary>
/// Updates entity trees by ticking any entity or behavior that implements
/// <see cref="IUpdatable"/>.
/// </summary>
public sealed class Updater
{
    /// <summary>
    /// Shared stateless updater used by built-in scene and playfield operations.
    /// </summary>
    public static Updater Default { get; } = new();

    /// <summary>
    /// Updates <paramref name="entity"/>, its behaviors, and any child entities.
    /// </summary>
    public void Update(IEntity entity, in EntityUpdateContext context)
    {
        UpdateEntity(entity, in context);

        if (entity is IUpdateTraversalOwner)
            return;

        if (entity is not IContainer container)
            return;

        for (int i = 0; i < container.Entities.Count; i++)
        {
            Update(container.Entities[i], in context);
        }
    }

    /// <summary>
    /// Updates <paramref name="entity"/> and its behaviors, but not child entities.
    /// Use when another operation owns child traversal.
    /// </summary>
    private void UpdateEntity(IEntity entity, in EntityUpdateContext context)
    {
        if (entity is IUpdateEnabled { Enabled: false })
            return;

        if (entity is IUpdatable updatable)
            updatable.Update(in context);

        UpdateBehaviors(entity, in context);
    }

    /// <summary>
    /// Updates all updatable behaviors attached to <paramref name="entity"/>.
    /// </summary>
    private void UpdateBehaviors(IEntity entity, in EntityUpdateContext context)
    {
        for (int i = 0; i < entity.Behaviors.Count; i++)
        {
            if (entity.Behaviors[i] is IUpdatable updatable)
                updatable.Update(in context);
        }
    }
}