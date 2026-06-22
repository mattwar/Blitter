using System.Numerics;


namespace Blitter.Blocks3D;

/// <summary>
/// Turns an entity so it faces a target each frame; another entity, a fixed world point, or a point chosen by a callback. 
/// The entity's local -Z axis (its "forward") is aimed at the target, matching Blitter's camera convention. 
/// A set-and-forget way to make turrets, enemies, signposts, or billboards track something without writing any rotation math.
/// </summary>
public sealed class LookAt3D : Behavior
{
    /// <summary>
    /// An entity to face. 
    /// </summary>
    public Entity? Target { get; set; }

    /// <summary>
    /// A fixed world-space point to face. Used when
    /// <see cref="Target"/> is not set.
    /// </summary>
    public Vector3? TargetPoint { get; set; }

    /// <summary>
    /// A callback that returns the world-space point to face, or
    /// <c>null</c> to skip turning this frame. Used when neither
    /// <see cref="Target"/> nor <see cref="TargetPoint"/> apply.
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

    private Transform3D _transform = null!;

    protected override void OnAttach(IEntity entity)
    {
        _transform = entity.GetOrAddTrait<Transform3D>();
    }

    public override void Apply(in UpdateContext context)
    {
        if (ResolveTarget() is not { } point)
            return;

        var toTarget = point - _transform.Position;
        if (KeepUpright)
            toTarget.Y = 0f;

        if (toTarget.LengthSquared() <= 1e-12f)
            return;

        var desired = MathG.LookRotation(toTarget, Up);

        if (TurnSpeed <= 0f)
        {
            _transform.Orientation = desired;
            return;
        }

        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f)
            return;

        var current = _transform.Orientation;

        // Shortest-arc angle between the current and desired orientations.
        var dot = Math.Clamp(MathF.Abs(Quaternion.Dot(current, desired)), -1f, 1f);
        var angle = 2f * MathF.Acos(dot);
        if (angle <= 1e-6f)
        {
            _transform.Orientation = desired;
            return;
        }

        var maxStep = TurnSpeed * dt;
        if (maxStep >= angle)
        {
            _transform.Orientation = desired;
            return;
        }

        var t = maxStep / angle;
        _transform.Orientation = Quaternion.Normalize(Quaternion.Slerp(current, desired, t));
    }

    // Picks the active target point following the documented priority:
    // a live TargetSprite, then TargetPoint, then TargetSelector.
    private Vector3? ResolveTarget()
    {
        if (Target is {} entity)
            return entity.GetOrAddTrait<Transform3D>().Position;

        if (TargetPoint is { } point)
            return point;

        return TargetSelector?.Invoke();
    }
}
