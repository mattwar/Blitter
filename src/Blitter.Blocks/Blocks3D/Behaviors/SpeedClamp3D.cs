using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// Clamps an entity's <see cref="Velocity3D"/> magnitude into the <c>[Min, Max]</c> range each frame, preserving direction. 
/// </summary>
public sealed class SpeedClamp3D : Behavior
{
    /// <summary>Lower speed bound. A moving sprite below this is pushed back up to it.</summary>
    public float Min { get; set; }

    /// <summary>Upper speed bound. A sprite faster than this is pulled back down to it.</summary>
    public float Max { get; set; } = float.PositiveInfinity;

    private Velocity3D _velocity = null!;

    protected override void OnAttach(IEntity entity)
    {
        _velocity = entity.GetOrAddTrait<Velocity3D>();
    }

    public override void Apply(in UpdateContext context)
    {
        var v = _velocity.Velocity;
        var speedSq = v.LengthSquared();
        if (speedSq == 0f)
            return;

        if (speedSq < Min * Min && Min > 0f)
        {
            var speed = MathF.Sqrt(speedSq);
            _velocity.Velocity = v * (Min / speed);
        }
        else if (speedSq > Max * Max)
        {
            var speed = MathF.Sqrt(speedSq);
            _velocity.Velocity = v * (Max / speed);
        }
    }
}
