using System.Numerics;

namespace Blitter.Blocks3D;

/// <summary>
/// Applies an entity's <see cref="Velocity3D.Velocity"/> into <see cref="Transform3D.Position"/> 
/// and its <see cref="Velocity3D.AngularVelocity"/> to <see cref="Transform3D.Orientation"/> each frame.
/// The 3D analog of <c>Blitter.Blocks2D.Motion2D</c>.
/// </summary>
public class Motion3D : Behavior, IUpdatable
{
    /// <summary>
    /// Minimum time that must accumulate between successful integration
    /// steps. Updates with smaller deltas are buffered and applied later.
    /// </summary>
    public TimeSpan MinUpdateInterval { get; set; } = TimeSpan.FromMilliseconds(1);

    // Accumulated time from Update calls that did not meet
    // MinUpdateInterval; carried forward to the next Update.
    private TimeSpan _pendingDelta;

    private Transform3D _transform = null!;
    private Velocity3D _velocity = null!;

    protected override void OnAttach(IEntity entity)
    {
        _transform = entity.GetOrAddTrait<Transform3D>();
        _velocity = entity.GetOrAddTrait<Velocity3D>();
    }

    public void Update(in UpdateContext context)
    {
        if (context.ElapsedSinceLastUpdate == TimeSpan.Zero)
            return;

        // Buffer small deltas so motion isn't lost to float rounding when
        // the host renders far faster than physics needs.
        _pendingDelta += context.ElapsedSinceLastUpdate;
        if (_pendingDelta < MinUpdateInterval)
            return;

        var timeDelta = _pendingDelta;
        _pendingDelta = TimeSpan.Zero;
        var dt = (float)timeDelta.TotalSeconds;

        var av = _velocity.AngularVelocity;
        var avLenSq = av.LengthSquared();
        if (avLenSq > 0f)
        {
            // AngularVelocity is an axis vector whose length is the
            // angular speed in radians per second. Compose the delta
            // rotation on the left so the integration is in the world
            // frame (matches the 2D Rotation semantics).
            var avLen = MathF.Sqrt(avLenSq);
            var axis = av / avLen;
            var delta = Quaternion.CreateFromAxisAngle(axis, avLen * dt);
            _transform.Orientation = Quaternion.Normalize(delta * _transform.Orientation);
        }

        if (_velocity.Velocity != Vector3.Zero)
        {
            _transform.Position += _velocity.Velocity * dt;
        }
    }
}
