using System.Numerics;

namespace Blitter.Blocks2D;

/// <summary>
/// Accelerates the sprite each tick by <see cref="Acceleration"/>.
/// Pair with <see cref="Motion2D"/> to actually integrate position.
/// </summary>
public sealed class Gravity2D : SpriteBehavior2D
{
    /// <summary>
    /// Acceleration in world units / s². Defaults to (0, 1500) — downward in screen space.
    /// </summary>
    public Vector2 Acceleration { get; set; } = new Vector2(0f, 1500f);

    /// <summary>
    /// Optional cap on the velocity component along <see cref="Acceleration"/>
    /// (i.e. fall speed). Zero leaves it uncapped. Doesn't constrain motion
    /// perpendicular to gravity, so horizontal speed is unaffected.
    /// </summary>
    public float MaxFallSpeed { get; set; }

    public override void Apply(Sprite2D target, in UpdateContext2D context)
    {
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f)
            return;

        var accel = Acceleration;
        if (accel.LengthSquared() <= float.Epsilon)
            return;

        var v = Sprite2D.GetVelocity(target.Speed, target.Heading);
        v += accel * dt;

        var cap = MaxFallSpeed;
        if (cap > 0f)
        {
            // Project v onto the unit gravity axis; if the component
            // along it exceeds the cap, trim only along that axis.
            var axis = Vector2.Normalize(accel);
            var along = Vector2.Dot(v, axis);
            if (along > cap)
                v -= axis * (along - cap);
        }

        (target.Speed, target.Heading) = Sprite2D.GetSpeedAndHeading(v);
    }
}
