namespace Blitter.Blocks2D;

/// <summary>
/// Reusable collision-space behavior for containers. Implements the
/// dimension-neutral <see cref="ICollisionSpace"/> so the core updater can
/// drive it, and owns the <see cref="Collider2D"/> that resolves its contacts.
/// </summary>
public sealed class CollisionSpace2D : Behavior, ICollisionSpace
{
    private readonly Collider2D _collider = new();

    private IContainer Container =>
        Entity as IContainer
            ?? throw new InvalidOperationException($"{nameof(CollisionSpace2D)} must be attached to an {nameof(IContainer)}.");

    /// <summary>
    /// Maximum number of collision substeps per frame. When the fastest
    /// collider would move more than half its hit radius in one frame,
    /// the updater runs update/collision multiple times with proportionally
    /// smaller deltas.
    /// </summary>
    public int MaxSubsteps { get; set; } = 1;

    /// <inheritdoc/>
    public void ResolveCollisions()
    {
        _collider.Collide(Container.Entities, Container.Contains);
    }

    /// <inheritdoc/>
    public int GetCollisionSubstepCount(in EntityUpdateContext context)
    {
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f || MaxSubsteps <= 1)
            return 1;

        float maxStep = 0f;
        float minRadius = float.PositiveInfinity;
        var entities = Container.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (!Container.Contains(entity) || entity is IColliderBarrier2D)
                continue;
            if (!Collider2D.TryGetHitShape(entity, out var posed))
                continue;
            var r = posed.BoundingCircle.Radius;
            if (r <= 0f)
                continue;
            if (r < minRadius)
                minRadius = r;
            if (!entity.TryGetTrait<Velocity2D>(out var velocity))
                continue;
            var step = MathF.Abs(velocity.Speed) * dt;
            if (step > maxStep)
                maxStep = step;
        }

        if (!float.IsFinite(minRadius) || minRadius <= 0f)
            return 1;
        var budget = 0.5f * minRadius;
        if (maxStep <= budget)
            return 1;
        int substeps = (int)MathF.Ceiling(maxStep / budget);
        return Math.Clamp(substeps, 1, MaxSubsteps);
    }
}