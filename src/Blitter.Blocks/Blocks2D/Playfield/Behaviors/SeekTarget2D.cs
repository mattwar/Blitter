using System.Numerics;

namespace Blitter.Blocks2D;
using Bits;


/// <summary>
/// Steers a sprite toward a target position. 
/// Each tick, rotates <see cref="Sprite2D.Heading"/> toward the target (capped by <see cref="MaxTurnRate"/>) 
/// and accelerates <see cref="Sprite2D.Speed"/> toward <see cref="MaxSpeed"/>. 
/// Composes with <see cref="Motion2D"/>, which actually integrates the updated heading + speed.
/// </summary>
public class SeekTarget2D : Behavior
{
    /// <summary>
    /// Returns the current world-space target, or <c>null</c> to stop
    /// steering this tick. Invoked once per update.
    /// </summary>
    public required Func<Vector2?> Target { get; init; }

    /// <summary>
    /// Maximum degrees of heading change per second.
    /// </summary>
    public float MaxTurnRate { get; set; } = 180f;

    /// <summary>
    /// Acceleration in world units per second² toward <see cref="MaxSpeed"/>.
    /// </summary>
    public float Acceleration { get; set; } = 200f;

    /// <summary>
    /// Upper bound on <see cref="Sprite2D.Speed"/>.
    /// </summary>
    public float MaxSpeed { get; set; } = 200f;

    /// <summary>
    /// Distance inside which steering / acceleration is suspended for
    /// the tick (the sprite coasts). <c>0</c> disables the dead-zone.
    /// </summary>
    public float ArriveRadius { get; set; } = 0f;


    private Velocity2D _velocity = null!;
    private Transform2D _transform = null!;

    protected override void OnAttach(IEntity entity)
    {
        _velocity = entity.GetOrAddTrait<Velocity2D>();
        _transform = entity.GetOrAddTrait<Transform2D>();
    }    

    public override void Apply(in UpdateContext context)
    {
        if (Target() is not Vector2 dest)
            return;

        var to = dest - _transform.Position;
        if (ArriveRadius > 0f && to.LengthSquared() <= ArriveRadius * ArriveRadius)
            return;

        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f)
            return;

        if (to.LengthSquared() > 1e-6f)
        {
            // Heading 0 = up (-Y), matching Sprite2D.GetVelocity.
            var desired = MathF.Atan2(to.X, -to.Y) * (180f / MathF.PI);
            var diff = WrapSigned(desired - _velocity.Heading);
            var maxStep = MaxTurnRate * dt;
            var step = Math.Clamp(diff, -maxStep, maxStep);
            _velocity.Heading = WrapDeg(_velocity.Heading + step);
        }

        _velocity.Speed = Math.Min(MaxSpeed, _velocity.Speed + Acceleration * dt);
    }

    // Wrap a delta to (-180, 180] so we always turn the short way around.
    private static float WrapSigned(float deg)
    {
        deg %= 360f;
        if (deg > 180f) deg -= 360f;
        else if (deg < -180f) deg += 360f;
        return deg;
    }

    private static float WrapDeg(float deg)
    {
        deg %= 360f;
        if (deg < 0f) deg += 360f;
        return deg;
    }
}
