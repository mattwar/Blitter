using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Discriminator for a <see cref="HitPrimitive3D"/>.
/// </summary>
public enum HitKind3D : byte
{
    /// <summary>
    /// Center + radius sphere (<see cref="HitPrimitive3D.P0"/> = center,
    /// <see cref="HitPrimitive3D.R"/> = radius).
    /// </summary>
    Sphere,

    /// <summary>
    /// Rounded line segment (<see cref="HitPrimitive3D.P0"/> and
    /// <see cref="HitPrimitive3D.P1"/> = endpoints,
    /// <see cref="HitPrimitive3D.R"/> = radius).
    /// </summary>
    Capsule,
}

/// <summary>
/// A single collidable 3D primitive. Stack-only by convention — built
/// by a <see cref="HitShape3D"/> into a <see cref="System.Span{T}"/>
/// and handed to a <see cref="HitTester3D"/>; never stored on the heap.
/// </summary>
public readonly struct HitPrimitive3D
{
    /// <summary>Which primitive shape this struct represents.</summary>
    public readonly HitKind3D Kind;

    /// <summary>Primary point. Sphere center / capsule endpoint A.</summary>
    public readonly Vector3 P0;

    /// <summary>
    /// Secondary point. Capsule endpoint B; unused for
    /// <see cref="HitKind3D.Sphere"/>.
    /// </summary>
    public readonly Vector3 P1;

    /// <summary>Scalar. Sphere radius / capsule radius.</summary>
    public readonly float R;

    private HitPrimitive3D(HitKind3D kind, Vector3 p0, Vector3 p1, float r)
    {
        Kind = kind;
        P0 = p0;
        P1 = p1;
        R = r;
    }

    /// <summary>
    /// Builds a sphere primitive centered on <paramref name="center"/>
    /// with radius <paramref name="radius"/>.
    /// </summary>
    public static HitPrimitive3D Sphere(Vector3 center, float radius) =>
        new(HitKind3D.Sphere, center, default, radius);

    /// <summary>
    /// Builds a capsule primitive — the Minkowski sum of the segment
    /// <paramref name="a"/>–<paramref name="b"/> with a ball of
    /// <paramref name="radius"/>. Degenerate endpoints
    /// (<paramref name="a"/> = <paramref name="b"/>) collide as a sphere.
    /// </summary>
    public static HitPrimitive3D Capsule(Vector3 a, Vector3 b, float radius) =>
        new(HitKind3D.Capsule, a, b, radius);

    /// <summary>True when this primitive overlaps <paramref name="other"/>.</summary>
    public bool Intersects(in HitPrimitive3D other)
    {
        return (Kind, other.Kind) switch
        {
            (HitKind3D.Sphere, HitKind3D.Sphere) => IntersectsSphereSphere(in this, in other),
            (HitKind3D.Sphere, HitKind3D.Capsule) => IntersectsSphereCapsule(in this, in other),
            (HitKind3D.Capsule, HitKind3D.Sphere) => IntersectsSphereCapsule(in other, in this),
            (HitKind3D.Capsule, HitKind3D.Capsule) => IntersectsCapsuleCapsule(in this, in other),
            _ => false,
        };
    }

    private static bool IntersectsSphereSphere(in HitPrimitive3D a, in HitPrimitive3D b)
    {
        var d = a.P0 - b.P0;
        var rs = a.R + b.R;
        return d.LengthSquared() <= rs * rs;
    }

    private static bool IntersectsSphereCapsule(in HitPrimitive3D sphere, in HitPrimitive3D capsule)
    {
        var distSq = PointSegmentDistanceSquared(sphere.P0, capsule.P0, capsule.P1);
        var rs = sphere.R + capsule.R;
        return distSq <= rs * rs;
    }

    private static bool IntersectsCapsuleCapsule(in HitPrimitive3D a, in HitPrimitive3D b)
    {
        var distSq = SegmentSegmentDistanceSquared(a.P0, a.P1, b.P0, b.P1);
        var rs = a.R + b.R;
        return distSq <= rs * rs;
    }

    private static float PointSegmentDistanceSquared(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var lenSq = ab.LengthSquared();
        if (lenSq <= float.Epsilon)
            return (p - a).LengthSquared();
        var t = Vector3.Dot(p - a, ab) / lenSq;
        if (t < 0f) t = 0f;
        else if (t > 1f) t = 1f;
        var closest = a + t * ab;
        return (p - closest).LengthSquared();
    }

    // Classic two-segment closest-point algorithm (Ericson, Real-Time
    // Collision Detection, ch. 5). Returns the squared distance between
    // the two segments' closest points.
    private static float SegmentSegmentDistanceSquared(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        var d1 = q1 - p1;
        var d2 = q2 - p2;
        var r  = p1 - p2;
        float a = Vector3.Dot(d1, d1);
        float e = Vector3.Dot(d2, d2);
        float f = Vector3.Dot(d2, r);

        const float eps = 1e-12f;

        float s, t;
        if (a <= eps && e <= eps)
            return (p1 - p2).LengthSquared();

        if (a <= eps)
        {
            s = 0f;
            t = Math.Clamp(f / e, 0f, 1f);
        }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= eps)
            {
                t = 0f;
                s = Math.Clamp(-c / a, 0f, 1f);
            }
            else
            {
                float b = Vector3.Dot(d1, d2);
                float denom = a * e - b * b;
                s = denom != 0f
                    ? Math.Clamp((b * f - c * e) / denom, 0f, 1f)
                    : 0f;
                t = (b * s + f) / e;
                if (t < 0f)
                {
                    t = 0f;
                    s = Math.Clamp(-c / a, 0f, 1f);
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = Math.Clamp((b - c) / a, 0f, 1f);
                }
            }
        }

        var c1 = p1 + d1 * s;
        var c2 = p2 + d2 * t;
        return (c1 - c2).LengthSquared();
    }
}
