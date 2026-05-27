using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// A static, non-sprite obstacle in a <see cref="PlayField3D"/>.
/// Participates in the collision pass: when a sprite's
/// <see cref="Sprite3D.HitSphere"/> overlaps the barrier's shape, the
/// playfield dispatches <see cref="Sprite3D.OnHitBarrier"/>.
/// </summary>
public abstract class Barrier3D
{
    /// <summary>
    /// True when <paramref name="sphere"/> overlaps this barrier's
    /// shape. Called once per barrier per frame during the playfield's
    /// collision pass.
    /// </summary>
    public abstract bool Intersects(BoundingSphere sphere);

    /// <summary>
    /// Called by <see cref="PlayField3D"/> once per frame. Useful for
    /// barriers that are animated or have variable properties over time.
    /// </summary>
    public virtual void Update(in UpdateContext3D context) { }

    /// <summary>
    /// Render this barrier. By default, barriers don't have a visual
    /// representation.
    /// </summary>
    public virtual void Draw(Renderer3D renderer) { }

    /// <summary>Called when <paramref name="hitter"/> collided with this barrier.</summary>
    public virtual void OnHitSprite(Sprite3D hitter, in UpdateContext3D context) { }
}
