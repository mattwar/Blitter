using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// A static, non-sprite obstacle in a <see cref="PlayField3D"/>.
/// Participates in the collision pass: when a sprite's
/// <see cref="Sprite3D.HitSphere"/> overlaps the barrier's shape, the
/// playfield dispatches <see cref="Sprite3D.OnHitBarrier"/>.
/// Unlike sprites, barriers do not collide with other barriers.
/// </summary>
public abstract class Barrier3D
{
    /// <summary>
    /// Optional visual rendered by the default <see cref="Draw"/> and used by the default <see cref="HitShape"/>.
    /// </summary>
    public Visual3D? Visual { get; set; }

    /// <summary>
    /// World-space position of the barrier's local origin.
    /// </summary>
    public Vector3 Position { get; set; }

    /// <summary>
    /// Orientation of the barrier's local axes in world space.
    /// </summary>
    public Quaternion Orientation { get; set; } = Quaternion.Identity;

    /// <summary>
    /// Uniform scale applied to the visual and the default hit shape.
    /// </summary>
    public float Scale { get; set; } = 1f;

    /// <summary>
    /// Per-channel tint multiplied into the visual at draw time.
    /// </summary>
    public Color Tint { get; set; } = Color.White;

    /// <summary>
    /// World-space collision shape: the current <see cref="Visual"/>'s
    /// <see cref="Visual3D.HitShape"/> combined with this barrier's
    /// <see cref="Position"/>, <see cref="Orientation"/>, and
    /// <see cref="Scale"/>. Override to substitute a different shape.
    /// </summary>
    public virtual PosedHitShape3D HitShape =>
        new(Visual?.HitShape ?? HitShape3D.None, new Pose3D(Position, Orientation, Scale));

    /// <summary>
    /// Called by <see cref="PlayField3D"/> once per frame.
    /// </summary>
    public virtual void Update(in UpdateContext3D context) { }

    /// <summary>
    /// Render this barrier. Default draws <see cref="Visual"/> at the
    /// barrier's pose, or does nothing if no visual is set.
    /// </summary>
    public virtual void Draw(Renderer3D renderer) =>
        Visual?.Draw(renderer, new Pose3D(Position, Orientation, Scale), Tint, TimeSpan.Zero);

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

