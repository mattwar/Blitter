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
    /// Per-frame hook for animated barriers (flippers, moving platforms,
    /// rotating obstacles). Called by the playfield before the sprite
    /// update and collision passes, so updated geometry is what sprites
    /// collide against this frame. Default is no-op.
    /// </summary>
    public virtual void Update(in UpdateContext2D context) { }
}
