namespace Blitter.Blocks2D;

/// <summary>
/// Provides a sprite's world-space collision shape
/// sourced from either the explicit <see cref="CollisionShape2D"/> trait or derived from the sprite's visual.
/// </summary>
public class ColliderShape2D : Behavior
{
    private IEntity _entity = null!;
    private Transform2D _transform = null!;
    private Appearance2D _appearance = null!;

    protected override void OnAttach(IEntity entity)
    {
        _entity = entity;
        _transform = entity.GetOrAddTrait<Transform2D>();
        _appearance = entity.GetOrAddTrait<Appearance2D>();
    }

    public override void Apply(in UpdateContext context)
    {
    }

    /// <summary>
    /// The sprite's world-space collision shape: the explicit
    /// <see cref="CollisionShape2D"/> override when supplied, otherwise the
    /// shape of the visual materialised from <see cref="Appearance2D.Source"/>,
    /// flipped and posed by the sprite's transform.
    /// </summary>
    public PosedHitShape2D GetShape()
    {
        var local =
            _entity.TryGetTrait<CollisionShape2D>(out var collision)
                && !ReferenceEquals(collision.Shape, HitShape2D.None)
                ? collision.Shape
                : _appearance.Source.GetComposedVisual()?.HitShape ?? HitShape2D.None;

        return new(local.Flipped(_appearance.Flipped), _transform.Pose);
    }
}
