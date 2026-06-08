using System.Numerics;

namespace Blitter.Blocks3D;

/// <summary>
/// Clamps a sprite's <see cref="Sprite3D.Velocity"/> magnitude into the
/// <c>[Min, Max]</c> range each frame, preserving direction. Useful as
/// the last behavior in a ball's chain so successive bounces and
/// barrier kicks can't let it slow to a crawl or run away.
/// </summary>
public sealed class SpeedClamp3D : SpriteBehavior3D
{
    /// <summary>Lower speed bound. A moving sprite below this is pushed back up to it.</summary>
    public float Min { get; set; }

    /// <summary>Upper speed bound. A sprite faster than this is pulled back down to it.</summary>
    public float Max { get; set; } = float.PositiveInfinity;

    public override void Apply(Sprite3D target, in UpdateContext3D context)
    {
        var v = target.Velocity;
        var speedSq = v.LengthSquared();
        if (speedSq == 0f)
            return;

        if (speedSq < Min * Min && Min > 0f)
        {
            var speed = MathF.Sqrt(speedSq);
            target.Velocity = v * (Min / speed);
        }
        else if (speedSq > Max * Max)
        {
            var speed = MathF.Sqrt(speedSq);
            target.Velocity = v * (Max / speed);
        }
    }
}
