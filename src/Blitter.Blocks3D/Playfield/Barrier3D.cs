using System.Numerics;

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
    /// Collision shape of this barrier in world space. 
    /// </summary>
    public abstract PosedHitShape3D HitShape { get; }

    /// <summary>
    /// Called by <see cref="PlayField3D"/> once per frame.
    /// </summary>
    public virtual void Update(in UpdateContext3D context) { }

    /// <summary>
    /// Render this barrier. 
    /// By default, barriers don't have a visual representation.
    /// </summary>
    public virtual void Draw(Renderer3D renderer) { }

    /// <summary>Called when a sprite collides with this barrier.</summary>
    public virtual void OnHitSprite(Sprite3D hitter, in UpdateContext3D context) { }

    /// <summary>
    /// Physical characteristics of this barrier.
    /// Used by gameplay mechanics.
    /// </summary>
    public virtual PhysicsMaterial PhysicsMaterial { get; set; } = PhysicsMaterial.Ideal;

    /// <summary>
    /// Surface velocity at <paramref name="point"/> in world units per second.
    /// Animated barriers (moving platforms, rotating fans)
    /// override to add their motion to the contact. Default returns
    /// <see cref="Vector3.Zero"/>.
    /// </summary>
    public virtual Vector3 SurfaceVelocityAt(Vector3 point) => Vector3.Zero;
}

