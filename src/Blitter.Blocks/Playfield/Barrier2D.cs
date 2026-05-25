using Blitter.Bits;

namespace Blitter.Blocks;

/// <summary>
/// A static, non-sprite obstacle in a <see cref="PlayField2D"/>.
/// Participates in the collision pass: when a sprite's
/// <see cref="Sprite2D.HitCircle"/> overlaps the barrier's shape, the
/// playfield dispatches <see cref="Sprite2D.OnHitBarrier"/>. Barriers
/// don't update, don't get reaped, and don't have behaviors.
/// </summary>
public abstract class Barrier2D
{
    /// <summary>
    /// True when <paramref name="circle"/> overlaps this barrier's
    /// shape. Called once per prop per tick during the playfield's
    /// collision pass.
    /// </summary>
    public abstract bool Intersects(BoundingCircle circle);

    /// <summary>
    /// Called by <see cref="PlayField2D"/> once per tick.
    /// Useful for barriers that are animated or have variable properties over time.
    /// </summary>
    public virtual void Update(in UpdateContext2D context) { }

    /// <summary>
    /// Render this barrier.
    /// By default, barriers don't have a visual representation.
    /// </summary>
    public virtual void Draw(Renderer2D renderer) { }

    /// <summary>
    /// Called when the <paramref name="hitter"/> collided with this barrier.
    /// </summary>
    public virtual void OnHitSprite(Sprite2D hitter, in UpdateContext2D context) { }

    /// <summary>
    /// Physical character of this barrier (elasticity, friction, kick).
    /// Read by <see cref="BarrierBounce2D"/> and composed with the
    /// behavior's ball-side knobs to determine each bounce. Default is
    /// <see cref="BarrierMaterial.Ideal"/>.
    /// </summary>
    public virtual BarrierMaterial Material { get; set; } = BarrierMaterial.Ideal;

    /// <summary>
    /// Surface velocity at <paramref name="point"/> in world units per
    /// second. Animated barriers (flippers, moving platforms) override
    /// to add their motion to the bounce. Default returns
    /// <see cref="System.Numerics.Vector2.Zero"/>.
    /// </summary>
    public virtual System.Numerics.Vector2 SurfaceVelocityAt(System.Numerics.Vector2 point)
        => System.Numerics.Vector2.Zero;
}
