using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Discriminator for a <see cref="HitPrimitive"/>.
/// </summary>
public enum HitKind : byte
{
    /// <summary>
    /// Center + radius circle (<see cref="HitPrimitive.P0"/> = center,
    /// <see cref="HitPrimitive.R"/> = radius).
    /// </summary>
    Circle,

    /// <summary>
    /// Rounded line segment (<see cref="HitPrimitive.P0"/> and
    /// <see cref="HitPrimitive.P1"/> = endpoints,
    /// <see cref="HitPrimitive.R"/> = radius).
    /// </summary>
    Capsule,
}

/// <summary>
/// A single collidable primitive. Stack-only by convention — built
/// by a <see cref="HitShape"/> into a <see cref="System.Span{T}"/>
/// and handed to a <see cref="Hitter"/>; never stored on the heap.
/// </summary>
public readonly struct HitPrimitive
{
    /// <summary>
    /// Which primitive shape this struct represents.
    /// </summary>
    public readonly HitKind Kind;

    /// <summary>
    /// Primary point. Circle center / capsule endpoint A.
    /// </summary>
    public readonly Vector2 P0;

    /// <summary>
    /// Secondary point. Capsule endpoint B; unused for
    /// <see cref="HitKind.Circle"/>.
    /// </summary>
    public readonly Vector2 P1;

    /// <summary>
    /// Scalar. Circle radius / capsule radius.
    /// </summary>
    public readonly float R;

    private HitPrimitive(HitKind kind, Vector2 p0, Vector2 p1, float r)
    {
        Kind = kind;
        P0 = p0;
        P1 = p1;
        R = r;
    }

    /// <summary>
    /// Builds a circle primitive centered on <paramref name="center"/>
    /// with radius <paramref name="radius"/>.
    /// </summary>
    public static HitPrimitive Circle(Vector2 center, float radius) =>
        new(HitKind.Circle, center, default, radius);

    /// <summary>
    /// Builds a capsule primitive — the Minkowski sum of the segment
    /// <paramref name="a"/>–<paramref name="b"/> with a disk of
    /// <paramref name="radius"/>. Degenerate endpoints
    /// (<paramref name="a"/> = <paramref name="b"/>) collide as a circle.
    /// </summary>
    public static HitPrimitive Capsule(Vector2 a, Vector2 b, float radius) =>
        new(HitKind.Capsule, a, b, radius);

    /// <summary>
    /// True when this primitive overlaps <paramref name="other"/>.
    /// </summary>
    public bool Intersects(in HitPrimitive other)
    {
        // Pair table. Each (kind, kind) case dispatches to a closed-form
        // test. Asymmetric pairs delegate to the reverse case so each
        // combination is implemented exactly once.
        return (Kind, other.Kind) switch
        {
            (HitKind.Circle, HitKind.Circle) => IntersectsCircleCircle(in this, in other),
            (HitKind.Circle, HitKind.Capsule) => IntersectsCircleCapsule(in this, in other),
            (HitKind.Capsule, HitKind.Circle) => IntersectsCircleCapsule(in other, in this),
            (HitKind.Capsule, HitKind.Capsule) => IntersectsCapsuleCapsule(in this, in other),
            _ => false,
        };
    }

    private static bool IntersectsCircleCircle(in HitPrimitive a, in HitPrimitive b)
    {
        var d = a.P0 - b.P0;
        var rs = a.R + b.R;
        return d.LengthSquared() <= rs * rs;
    }

    private static bool IntersectsCircleCapsule(in HitPrimitive circle, in HitPrimitive capsule)
    {
        var distSq = PointSegmentDistanceSquared(circle.P0, capsule.P0, capsule.P1);
        var rs = circle.R + capsule.R;
        return distSq <= rs * rs;
    }

    private static bool IntersectsCapsuleCapsule(in HitPrimitive a, in HitPrimitive b)
    {
        var distSq = SegmentSegmentDistanceSquared(a.P0, a.P1, b.P0, b.P1);
        var rs = a.R + b.R;
        return distSq <= rs * rs;
    }

    private static float PointSegmentDistanceSquared(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lenSq = ab.LengthSquared();
        if (lenSq <= float.Epsilon)
            return (p - a).LengthSquared();
        var t = Vector2.Dot(p - a, ab) / lenSq;
        if (t < 0f) t = 0f;
        else if (t > 1f) t = 1f;
        var closest = a + t * ab;
        return (p - closest).LengthSquared();
    }

    private static float SegmentSegmentDistanceSquared(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        // 2D shortcut: if the open segments cross the distance is zero;
        // otherwise the minimum is achieved at an endpoint of one
        // segment projected onto the other.
        if (SegmentsCross(a1, a2, b1, b2))
            return 0f;
        var d1 = PointSegmentDistanceSquared(a1, b1, b2);
        var d2 = PointSegmentDistanceSquared(a2, b1, b2);
        var d3 = PointSegmentDistanceSquared(b1, a1, a2);
        var d4 = PointSegmentDistanceSquared(b2, a1, a2);
        return MathF.Min(MathF.Min(d1, d2), MathF.Min(d3, d4));
    }

    private static bool SegmentsCross(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        // Standard signed-area test. Returns true only for proper
        // (transverse) intersection; collinear / touching-at-endpoint
        // cases fall through to the endpoint-distance path above.
        float d1 = Cross(b2 - b1, a1 - b1);
        float d2 = Cross(b2 - b1, a2 - b1);
        float d3 = Cross(a2 - a1, b1 - a1);
        float d4 = Cross(a2 - a1, b2 - a1);
        return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f))
            && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
    }

    private static float Cross(Vector2 u, Vector2 v) => u.X * v.Y - u.Y * v.X;
}
