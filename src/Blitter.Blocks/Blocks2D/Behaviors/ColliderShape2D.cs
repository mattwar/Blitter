namespace Blitter.Blocks2D;

/// <summary>
/// Provides an entity's world-space collision shape, sourced from either the
/// explicit <see cref="CollisionShape2D"/> trait or — when the entity presents
/// a visual — derived from that visual. Shared by <see cref="Sprite2D"/> and
/// <see cref="Barrier2D"/>; the <see cref="Appearance2D"/> trait is optional, so
/// visual-less collidables (barriers) supply their geometry through
/// <see cref="CollisionShape2D"/> alone.
/// </summary>
public class ColliderShape2D : Behavior
{
    private IEntity _entity = null!;
    private Transform2D _transform = null!;

    protected override void OnAttach(IEntity entity)
    {
        _entity = entity;
        _transform = entity.GetOrAddTrait<Transform2D>();
    }

    public override void Apply(in UpdateContext context)
    {
    }

    /// <summary>
    /// The entity's world-space collision shape: the explicit
    /// <see cref="CollisionShape2D"/> override when supplied, otherwise the
    /// shape of the visual materialised from <see cref="Appearance2D.Source"/>
    /// (when an <see cref="Appearance2D"/> is present), flipped and posed by the
    /// entity's transform.
    /// </summary>
    public PosedHitShape2D GetShape()
    {
        _entity.TryGetTrait<Appearance2D>(out var appearance);
        var flip = appearance?.Flipped ?? FlipMode.None;

        var local =
            _entity.TryGetTrait<CollisionShape2D>(out var collision)
                && !ReferenceEquals(collision.Shape, HitShape2D.None)
                ? collision.Shape
                : appearance?.Source.GetComposedVisual()?.HitShape ?? HitShape2D.None;

        return new(local.Flipped(flip), _transform.Pose);
    }
}
