namespace Blitter.Blocks2D;

/// <summary>
/// A 2D collision scope whose entities are collided independently from sibling scopes.
/// </summary>
public interface ICollisionSpace2D
{
    /// <summary>
    /// The entities participating in this collision space.
    /// </summary>
    IReadOnlyList<IEntity> CollisionEntities { get; }

    /// <summary>
    /// Returns whether <paramref name="entity"/> is live for the current
    /// collision pass.
    /// </summary>
    bool IsCollisionLive(IEntity entity);
}

/// <summary>
/// Reusable collision-space behavior for ordinary containers.
/// </summary>
public sealed class CollisionSpace2D : Behavior, ICollisionSpace2D
{
    private IContainer Container =>
        Entity as IContainer
            ?? throw new InvalidOperationException($"{nameof(CollisionSpace2D)} must be attached to an {nameof(IContainer)}.");

    /// <inheritdoc/>
    public IReadOnlyList<IEntity> CollisionEntities => Container.Entities;

    /// <inheritdoc/>
    public bool IsCollisionLive(IEntity entity) => Container.Contains(entity);
}