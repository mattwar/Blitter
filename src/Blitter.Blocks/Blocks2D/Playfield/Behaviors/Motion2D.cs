namespace Blitter.Blocks2D;

/// <summary>
/// Integrates a sprite's <see cref="Sprite2D.Speed"/> /
/// <see cref="Sprite2D.Heading"/> into <see cref="Sprite2D.Center"/>
/// and its <see cref="Sprite2D.RotationSpeed"/>
/// into <see cref="Sprite2D.Rotation"/> each tick.
/// </summary>
public class Motion2D : Behavior
{
    /// <summary>
    /// Minimum time that must accumulate between successful integration
    /// steps. Updates with smaller deltas are buffered and applied later.
    /// </summary>
    public TimeSpan MinUpdateInterval { get; set; } = TimeSpan.FromMilliseconds(1);

    // Accumulated time from Update calls that did not meet
    // MinUpdateInterval; carried forward to the next Update.
    private TimeSpan _pendingDelta;

    private Transform2D _transform = null!;
    private Velocity2D _velocity = null!;

    protected override void OnAttach(IEntity entity)
    {
        _velocity = entity.GetOrAddTrait<Velocity2D>();
        _transform = entity.GetOrAddTrait<Transform2D>();
    }

    public override void Apply(in UpdateContext context)
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

        if (_velocity.RotationSpeed != 0f)
        {
            var rotationDelta = (float)(_velocity.RotationSpeed * timeDelta.TotalSeconds);
            _transform.Rotation = (_transform.Rotation + rotationDelta) % 360f;
        }

        if (_velocity.Speed != 0f)
        {
            var v = _velocity.Vector;
            _transform.Position += v * (float)timeDelta.TotalSeconds;
        }
    }
}
