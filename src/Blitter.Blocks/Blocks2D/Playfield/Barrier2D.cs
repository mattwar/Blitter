namespace Blitter.Blocks2D;

/// <summary>
/// An obstacle in a <see cref="PlayField2D"/>.
/// Barriers collide with sprites, but not each other.
/// </summary>
/// <remarks>
/// Like <see cref="Sprite2D"/>, a barrier is a convenience template that wires
/// up the traits and behaviors that make it collidable: a
/// <see cref="Transform2D"/> for placement plus a <see cref="ColliderShape2D"/>
/// behavior that poses an optional <see cref="CollisionShape2D"/> trait into the
/// world-space <see cref="HitShape"/>. Subclasses populate those traits rather
/// than re-implementing collision geometry.
/// </remarks>
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
    /// Physical characteristics of this barrier.
    /// </summary>
    public virtual PhysicsMaterial PhysicsMaterial { get; set; } = PhysicsMaterial.Ideal;

    /// <summary>
    /// Surface velocity at <paramref name="point"/> in world units per second. 
    /// </summary>
    public virtual System.Numerics.Vector2 SurfaceVelocityAt(System.Numerics.Vector2 point)
        => System.Numerics.Vector2.Zero;
}
