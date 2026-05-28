using System.Numerics;

namespace Blitter.Blocks3D;

/// <summary>
/// Accelerates the sprite each frame by <see cref="Acceleration"/>.
/// Pair with <see cref="Motion3D"/> to actually integrate position.
/// The 3D analog of <c>Blitter.Blocks2D.Gravity2D</c>.
/// </summary>
public sealed class Gravity3D : SpriteBehavior3D
{
    /// <summary>
    /// Acceleration in world units / s². Defaults to (0, -9.81, 0) —
    /// downward with Y up.
    /// </summary>
    public Vector3 Acceleration { get; set; } = new Vector3(0f, -9.81f, 0f);

    /// <summary>
    /// Optional cap on the velocity component along <see cref="Acceleration"/>
    /// (i.e. fall speed). Zero leaves it uncapped. Doesn't constrain
    /// motion perpendicular to gravity.
    /// </summary>
    public float MaxFallSpeed { get; set; }

    public override void Apply(Sprite3D target, in UpdateContext3D context)
    {
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f)
            return;

        var accel = Acceleration;
        if (accel.LengthSquared() <= float.Epsilon)
            return;

        var v = target.Velocity + accel * dt;

        var cap = MaxFallSpeed;
        if (cap > 0f)
        {
            // Project v onto the unit gravity axis; if the component
            // along it exceeds the cap, trim only along that axis.
            var axis = Vector3.Normalize(accel);
            var along = Vector3.Dot(v, axis);
            if (along > cap)
                v -= axis * (along - cap);
        }

        target.Velocity = v;
    }
}
