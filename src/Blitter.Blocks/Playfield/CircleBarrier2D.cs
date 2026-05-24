using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks;

/// <summary>
/// A circular barrier — pinball bumpers, posts, round corners.
/// </summary>
public class CircleBarrier2D : Barrier2D
{
    public Vector2 Center { get; }
    public float Radius { get; }

    public CircleBarrier2D(Vector2 center, float radius)
    {
        Center = center;
        Radius = radius < 0f ? 0f : radius;
    }

    public CircleBarrier2D(float x, float y, float radius)
        : this(new Vector2(x, y), radius) { }

    public override bool Intersects(BoundingCircle circle)
    {
        if (circle.IsEmpty)
            return false;
        var r = Radius + circle.Radius;
        return Vector2.DistanceSquared(Center, circle.Center) <= r * r;
    }
}
