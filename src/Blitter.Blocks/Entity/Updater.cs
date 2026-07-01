namespace Blitter.Blocks;

/// <summary>
/// Updates entity trees by ticking any entity or behavior that implements
/// <see cref="IUpdatable"/>.
/// </summary>
public sealed class Updater
{
    /// <summary>
    /// Shared updater used by built-in scene and playfield operations.
    /// </summary>
    public static Updater Default { get; } = new();

    /// <summary>
    /// Updates <paramref name="entity"/>, its behaviors, and any child entities.
    /// </summary>
    public void Update(IEntity entity, in EntityUpdateContext context)
    {
        if (entity.TryGetCapability<IUpdatability>(out var updatability) && !updatability.Enabled)
            return;

        var ownsTraversal = entity is IUpdatable;
        if (ownsTraversal)
        {
            UpdateEntity(entity, in context);
            return;
        }

        if (entity is IContainer collisionContainer 
            && entity.TryGetCapability<ICollisionSpace>(out var collisionSpace))
        {
            UpdateCollisionSpace(collisionContainer, collisionSpace, in context);
            UpdateBehaviors(entity, in context);
            return;
        }

        UpdateBehaviors(entity, in context);

        if (entity is IContainer container)
            UpdateChildren(container, in context);
    }

    private void UpdateCollisionSpace(IContainer container, ICollisionSpace collisionSpace, in EntityUpdateContext context)
    {
        var substeps = Math.Max(1, collisionSpace.GetCollisionSubstepCount(in context));
        var subContext = substeps > 1
            ? context with { ElapsedSinceLastUpdate = context.ElapsedSinceLastUpdate / substeps }
            : context;

        var deferredContainer = container as IDeferredMutationContainer;
        if (deferredContainer is not null)
            deferredContainer.BeginMutationBuffer();

        try
        {
            for (int i = 0; i < substeps; i++)
            {
                UpdateCollisionSpaceChildren(container, in subContext);
                collisionSpace.ResolveCollisions();
            }
        }
        finally
        {
            if (deferredContainer is not null)
                deferredContainer.EndMutationBuffer();
        }
    }

    private void UpdateCollisionSpaceChildren(IContainer container, in EntityUpdateContext context)
    {
        for (int i = 0; i < container.Entities.Count; i++)
        {
            var entity = container.Entities[i];
            if (container.Contains(entity))
                Update(entity, in context);
        }
    }

    private void UpdateChildren(IContainer container, in EntityUpdateContext context)
    {
        for (int i = 0; i < container.Entities.Count; i++)
            Update(container.Entities[i], in context);
    }

    /// <summary>
    /// Updates <paramref name="entity"/> and its behaviors, but not child entities.
    /// Use when another operation owns child traversal.
    /// </summary>
    private void UpdateEntity(IEntity entity, in EntityUpdateContext context)
    {
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