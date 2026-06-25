namespace Blitter.Blocks2D;

/// <summary>
/// An obstacle in a <see cref="PlayField2D"/>.
/// Barriers collide with sprites, but not each other.
/// </summary>
public abstract class Barrier2D : Entity, IColliderBarrier2D, IDrawable2D
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
        this.GetOrAddTrait<Surface2D>();
        if (!this.TryGetCapability<IColliderShape2D>(out _))
            this.GetOrAddBehavior<DefaultColliderShape2D>();
        base.OnAttach(entity);
    }

    /// <summary>
    /// Collision shape of this barrier in world space, provided by its
    /// <see cref="IColliderShape2D"/> behavior from the
    /// <see cref="CollisionShape2D"/> trait posed by <see cref="Transform"/>.
    /// Subclasses whose geometry doesn't fit a posed local shape may override
    /// this directly.
    /// </summary>
    public PosedHitShape2D HitShape =>
        this.TryGetCapability<IColliderShape2D>(out var collider)
            ? collider.GetShape()
            : new(HitShape2D.None, Transform.Pose);

    /// <summary>
    /// Render this barrier.
    /// By default, barriers don't have a visual representation.
    /// </summary>
    public virtual void Draw(Renderer2D renderer) { }

    /// <summary>
    /// Physical characteristics of this barrier, backed by a
    /// <see cref="Surface2D"/> trait that every barrier carries (added on
    /// attach, defaulting to <see cref="PhysicsMaterial.Ideal"/>).
    /// </summary>
    public PhysicsMaterial PhysicsMaterial
    {
        get => this.GetOrAddTrait<Surface2D>().Material;
        set => this.GetOrAddTrait<Surface2D>().Material = value;
    }
}
