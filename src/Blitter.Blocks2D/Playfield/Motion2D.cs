namespace Blitter.Blocks2D;

/// <summary>
/// Integrates a sprite's <see cref="Sprite2D.Speed"/> /
/// <see cref="Sprite2D.Heading"/> into <see cref="Sprite2D.Center"/>
/// and its <see cref="Sprite2D.RotationSpeed"/>
/// into <see cref="Sprite2D.Rotation"/> each tick.
/// </summary>
public class Motion2D : SpriteBehavior2D
{
    /// <summary>
    /// Minimum time that must accumulate between successful integration
    /// steps. Updates with smaller deltas are buffered and applied later.
    /// </summary>
    public TimeSpan MinUpdateInterval { get; set; } = TimeSpan.FromMilliseconds(1);

    // Accumulated time from Update calls that did not meet
    // MinUpdateInterval; carried forward to the next Update.
    private TimeSpan _pendingDelta;

    public override void Apply(Sprite2D target, in UpdateContext2D context)
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

        if (target.RotationSpeed != 0f)
        {
            var rotationDelta = (float)(target.RotationSpeed * timeDelta.TotalSeconds);
            target.Rotation = (target.Rotation + rotationDelta) % 360f;
        }

        if (target.Speed != 0f)
        {
            var v = Sprite2D.GetVelocity(target.Speed, target.Heading);
            target.Center += v * (float)timeDelta.TotalSeconds;
        }
    }
}
