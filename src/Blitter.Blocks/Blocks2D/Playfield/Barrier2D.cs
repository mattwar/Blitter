namespace Blitter.Blocks2D;

/// <summary>
/// An obstacle in a <see cref="PlayField2D"/>.
/// Barriers collide with sprites, but not each other.
/// </summary>
public abstract class Barrier2D : Entity
{
    private Transform2D? _transform;

    /// <summary>
    /// World-space placement (position, rotation, scale) of this barrier.
    /// </summary>
    public Transform2D Transform => _transform ??= this.GetOrAddTrait<Transform2D>();

    /// <inheritdoc/>
    protected override void OnAttach(IEntity entity)
    {
        _transform = this.GetOrAddTrait<Transform2D>();
        if (!this.TryGetBehavior<ColliderShape2D>(out _))
            this.AddBehavior(new ColliderShape2D());
        base.OnAttach(entity);
    }

    /// <summary>
    /// Collision shape of this barrier in world space, provided by its
    /// <see cref="ColliderShape2D"/> behavior from the
    /// <see cref="CollisionShape2D"/> trait posed by <see cref="Transform"/>.
    /// Subclasses whose geometry doesn't fit a posed local shape may override
    /// this directly.
    /// </summary>
    public virtual PosedHitShape2D HitShape =>
        this.TryGetBehavior<ColliderShape2D>(out var collider)
            ? collider.GetShape()
            : new(HitShape2D.None, Transform.Pose);

    /// <summary>
    /// Render this barrier.
    /// By default, barriers don't have a visual representation.
    /// </summary>
    public virtual void Draw(Renderer2D renderer) { }

    /// <summary>
    /// Called when the <paramref name="hitter"/> collided with this barrier.
    /// </summary>
    public virtual void OnHitSprite(Sprite2D hitter, in UpdateContext context) { }

    /// <summary>
    /// Physical characteristics of this barrier, backed by a
    /// <see cref="Surface2D"/> trait. Absent trait means
    /// <see cref="PhysicsMaterial.Ideal"/>.
    /// </summary>
    public PhysicsMaterial PhysicsMaterial
    {
        get => this.GetOrAddTrait<Surface2D>().Material;
        set => this.GetOrAddTrait<Surface2D>().Material = value;
    }
}
