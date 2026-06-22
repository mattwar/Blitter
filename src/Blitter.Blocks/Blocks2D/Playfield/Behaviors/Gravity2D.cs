using System.Numerics;

namespace Blitter.Blocks2D;
using Bits;


/// <summary>
/// Accelerates the sprite each tick by <see cref="Acceleration"/>.
/// Pair with <see cref="Motion2D"/> to actually integrate position.
/// </summary>
public sealed class Gravity2D : Behavior
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

    private Velocity2D? _velocity;

    public override void Apply(in UpdateContext context)
    {
        if (this.Entity is not IEntity entity)
            return;

        _velocity ??= entity.GetOrAddTrait<Velocity2D>();

        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f)
            return;

        var accel = Acceleration;
        if (accel.LengthSquared() <= float.Epsilon)
            return;

        var v = _velocity.Vector;
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

        _velocity.Vector = v;
    }
}
