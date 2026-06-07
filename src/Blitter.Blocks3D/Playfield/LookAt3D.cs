using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// Turns a sprite so it faces a target each frame — another sprite, a
/// fixed world point, or a point chosen by a callback. The sprite's
/// local -Z axis (its "forward") is aimed at the target, matching
/// Blitter's camera convention. A set-and-forget way to make turrets,
/// enemies, signposts, or billboards track something without writing
/// any rotation math.
/// </summary>
/// <remarks>
/// This behavior writes <see cref="Sprite3D.Orientation"/> directly. If
/// the sprite also has a <see cref="Motion3D"/> behavior that integrates
/// <see cref="Sprite3D.AngularVelocity"/>, place this behavior after it
/// in the sprite's <see cref="Sprite3D.Behaviors"/> list so the aim wins.
/// </remarks>
public sealed class LookAt3D : SpriteBehavior3D
{
    /// <summary>
    /// A sprite to face. When set, its <see cref="Sprite3D.Position"/> is
    /// used as the target each frame. Takes priority over
    /// <see cref="TargetPoint"/> and <see cref="TargetSelector"/> while
    /// the sprite is alive.
    /// </summary>
    public Sprite3D? TargetSprite { get; set; }

    /// <summary>
    /// A fixed world-space point to face. Used when
    /// <see cref="TargetSprite"/> is not set (or no longer alive).
    /// </summary>
    public Vector3? TargetPoint { get; set; }

    /// <summary>
    /// A callback that returns the world-space point to face, or
    /// <c>null</c> to skip turning this frame. Used when neither
    /// <see cref="TargetSprite"/> nor <see cref="TargetPoint"/> apply.
    /// </summary>
    public Func<Vector3?>? TargetSelector { get; set; }

    /// <summary>
    /// Direction the sprite treats as "up" while turning. Defaults to
    /// <see cref="Vector3.UnitY"/>. Keeps the sprite from rolling as it
    /// tracks the target.
    /// </summary>
    public Vector3 Up { get; set; } = Vector3.UnitY;

    /// <summary>
    /// When <c>true</c>, the sprite only turns left/right (yaw) and stays
    /// level, ignoring any height difference to the target. Handy for
    /// ground enemies or upright signs that shouldn't tilt up or down.
    /// </summary>
    public bool KeepUpright { get; set; }

    /// <summary>
    /// Maximum turn rate in radians per second. Zero (the default) snaps
    /// instantly to face the target; a positive value eases the sprite
    /// toward the target for a smoother, more natural turn.
    /// </summary>
    public float TurnSpeed { get; set; }

    public override void Apply(Sprite3D target, in UpdateContext3D context)
    {
        if (ResolveTarget() is not { } point)
            return;

        var toTarget = point - target.Position;
        if (KeepUpright)
            toTarget.Y = 0f;

        if (toTarget.LengthSquared() <= 1e-12f)
            return;

        var desired = MathG.LookRotation(toTarget, Up);

        if (TurnSpeed <= 0f)
        {
            target.Orientation = desired;
            return;
        }

        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f)
            return;

        var current = target.Orientation;

        // Shortest-arc angle between the current and desired orientations.
        var dot = Math.Clamp(MathF.Abs(Quaternion.Dot(current, desired)), -1f, 1f);
        var angle = 2f * MathF.Acos(dot);
        if (angle <= 1e-6f)
        {
            target.Orientation = desired;
            return;
        }

        var maxStep = TurnSpeed * dt;
        if (maxStep >= angle)
        {
            target.Orientation = desired;
            return;
        }

        var t = maxStep / angle;
        target.Orientation = Quaternion.Normalize(Quaternion.Slerp(current, desired, t));
    }

    // Picks the active target point following the documented priority:
    // a live TargetSprite, then TargetPoint, then TargetSelector.
    private Vector3? ResolveTarget()
    {
        if (TargetSprite is { IsAlive: true } sprite)
            return sprite.Position;

        if (TargetPoint is { } point)
            return point;

        return TargetSelector?.Invoke();
    }
}
