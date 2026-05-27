using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// 3D geometric primitives (closest-point queries, intersections, etc.)
/// that aren't covered by <see cref="System.Numerics"/> or
/// <see cref="MathG"/>. Pure functions over <see cref="Vector3"/> —
/// no allocation, no state.
/// </summary>
public static class Geometry3D
{
    /// <summary>
    /// Returns the point on triangle <c>ABC</c> closest to
    /// <paramref name="p"/>. The result lies in the triangle's plane,
    /// clamped to the interior or an edge. Works for any winding.
    /// Reference: Ericson, <i>Real-Time Collision Detection</i>, §5.1.5.
    /// </summary>
    public static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        var ab = b - a;
        var ac = c - a;
        var ap = p - a;

        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return a;  // vertex region A

        var bp = p - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return b;  // vertex region B

        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        {
            float v = d1 / (d1 - d3);
            return a + v * ab;  // edge AB
        }

        var cp = p - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return c;  // vertex region C

        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        {
            float w = d2 / (d2 - d6);
            return a + w * ac;  // edge AC
        }

        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + w * (c - b);  // edge BC
        }

        // Face region — express P in barycentric coordinates.
        float denom = 1f / (va + vb + vc);
        float vF = vb * denom;
        float wF = vc * denom;
        return a + ab * vF + ac * wF;
    }

    /// <summary>
    /// Squared distance from point <paramref name="p"/> to segment
    /// <paramref name="a"/>–<paramref name="b"/>. Returns 0 when the
    /// point lies on the segment.
    /// </summary>
    public static float PointSegmentDistanceSquared(Vector3 p, Vector3 a, Vector3 b)
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

    /// <summary>
    /// Squared distance between two finite segments. Reference:
    /// Ericson, <i>Real-Time Collision Detection</i>, §5.1.9.
    /// </summary>
    public static float SegmentSegmentDistanceSquared(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        var d1 = q1 - p1;
        var d2 = q2 - p2;
        var r = p1 - p2;
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

    /// <summary>
    /// True when the segment <paramref name="a"/>–<paramref name="b"/>
    /// (in box-local coordinates) intersects the axis-aligned box
    /// centered at the origin with the given half-extents. Standard
    /// Liang–Barsky / slab test.
    /// </summary>
    public static bool SegmentIntersectsAabb(Vector3 a, Vector3 b, Vector3 halfExtents)
    {
        var d = b - a;
        float tMin = 0f, tMax = 1f;

        for (int i = 0; i < 3; i++)
        {
            float ai = Component(a, i);
            float di = Component(d, i);
            float hi = Component(halfExtents, i);

            if (MathF.Abs(di) < 1e-12f)
            {
                if (MathF.Abs(ai) > hi)
                    return false;
                continue;
            }

            float t1 = (-hi - ai) / di;
            float t2 = (+hi - ai) / di;
            if (t1 > t2) (t1, t2) = (t2, t1);

            if (t1 > tMin) tMin = t1;
            if (t2 < tMax) tMax = t2;
            if (tMin > tMax) return false;
        }

        return true;
    }

    /// <summary>
    /// Tests two oriented bounding boxes (OBBs) for overlap using the
    /// 15-axis Separating Axis Theorem. Each box is given by its world
    /// center, rotation, and half-extents along its local axes.
    /// Reference: Ericson, <i>Real-Time Collision Detection</i>, §4.4.
    /// </summary>
    public static bool BoxesOverlap(
        Vector3 centerA, Quaternion rotationA, Vector3 halfExtentsA,
        Vector3 centerB, Quaternion rotationB, Vector3 halfExtentsB)
    {
        // Build each box's local axes from its rotation. ax/ay/az
        // are the world-space directions of A's local X/Y/Z.
        var ax = Vector3.Transform(Vector3.UnitX, rotationA);
        var ay = Vector3.Transform(Vector3.UnitY, rotationA);
        var az = Vector3.Transform(Vector3.UnitZ, rotationA);
        var bx = Vector3.Transform(Vector3.UnitX, rotationB);
        var by = Vector3.Transform(Vector3.UnitY, rotationB);
        var bz = Vector3.Transform(Vector3.UnitZ, rotationB);

        // Translation from A's center to B's center in A's frame.
        var t = centerB - centerA;
        var tA = new Vector3(Vector3.Dot(t, ax), Vector3.Dot(t, ay), Vector3.Dot(t, az));

        // Rotation matrix expressing B's axes in A's frame; absR for
        // the projection radii (with an epsilon to guard near-parallel
        // edges, per Ericson).
        Span<Vector3> R = stackalloc Vector3[3];
        Span<Vector3> absR = stackalloc Vector3[3];
        R[0] = new Vector3(Vector3.Dot(ax, bx), Vector3.Dot(ax, by), Vector3.Dot(ax, bz));
        R[1] = new Vector3(Vector3.Dot(ay, bx), Vector3.Dot(ay, by), Vector3.Dot(ay, bz));
        R[2] = new Vector3(Vector3.Dot(az, bx), Vector3.Dot(az, by), Vector3.Dot(az, bz));
        const float eps = 1e-6f;
        for (int i = 0; i < 3; i++)
            absR[i] = new Vector3(
                MathF.Abs(R[i].X) + eps,
                MathF.Abs(R[i].Y) + eps,
                MathF.Abs(R[i].Z) + eps);

        var a = halfExtentsA;
        var b = halfExtentsB;

        // Test the 3 face axes of A.
        for (int i = 0; i < 3; i++)
        {
            float ra = Component(a, i);
            float rb = b.X * Component(absR[i], 0) + b.Y * Component(absR[i], 1) + b.Z * Component(absR[i], 2);
            if (MathF.Abs(Component(tA, i)) > ra + rb) return false;
        }

        // Test the 3 face axes of B.
        for (int j = 0; j < 3; j++)
        {
            float ra = a.X * Component(absR[0], j) + a.Y * Component(absR[1], j) + a.Z * Component(absR[2], j);
            float rb = Component(b, j);
            float tProj = tA.X * Component(R[0], j) + tA.Y * Component(R[1], j) + tA.Z * Component(R[2], j);
            if (MathF.Abs(tProj) > ra + rb) return false;
        }

        // 9 edge–edge axes (A's edges × B's edges).
        // L = A0 × B0
        {
            float ra = a.Y * absR[2].X + a.Z * absR[1].X;
            float rb = b.Y * absR[0].Z + b.Z * absR[0].Y;
            if (MathF.Abs(tA.Z * R[1].X - tA.Y * R[2].X) > ra + rb) return false;
        }
        // L = A0 × B1
        {
            float ra = a.Y * absR[2].Y + a.Z * absR[1].Y;
            float rb = b.X * absR[0].Z + b.Z * absR[0].X;
            if (MathF.Abs(tA.Z * R[1].Y - tA.Y * R[2].Y) > ra + rb) return false;
        }
        // L = A0 × B2
        {
            float ra = a.Y * absR[2].Z + a.Z * absR[1].Z;
            float rb = b.X * absR[0].Y + b.Y * absR[0].X;
            if (MathF.Abs(tA.Z * R[1].Z - tA.Y * R[2].Z) > ra + rb) return false;
        }
        // L = A1 × B0
        {
            float ra = a.X * absR[2].X + a.Z * absR[0].X;
            float rb = b.Y * absR[1].Z + b.Z * absR[1].Y;
            if (MathF.Abs(tA.X * R[2].X - tA.Z * R[0].X) > ra + rb) return false;
        }
        // L = A1 × B1
        {
            float ra = a.X * absR[2].Y + a.Z * absR[0].Y;
            float rb = b.X * absR[1].Z + b.Z * absR[1].X;
            if (MathF.Abs(tA.X * R[2].Y - tA.Z * R[0].Y) > ra + rb) return false;
        }
        // L = A1 × B2
        {
            float ra = a.X * absR[2].Z + a.Z * absR[0].Z;
            float rb = b.X * absR[1].Y + b.Y * absR[1].X;
            if (MathF.Abs(tA.X * R[2].Z - tA.Z * R[0].Z) > ra + rb) return false;
        }
        // L = A2 × B0
        {
            float ra = a.X * absR[1].X + a.Y * absR[0].X;
            float rb = b.Y * absR[2].Z + b.Z * absR[2].Y;
            if (MathF.Abs(tA.Y * R[0].X - tA.X * R[1].X) > ra + rb) return false;
        }
        // L = A2 × B1
        {
            float ra = a.X * absR[1].Y + a.Y * absR[0].Y;
            float rb = b.X * absR[2].Z + b.Z * absR[2].X;
            if (MathF.Abs(tA.Y * R[0].Y - tA.X * R[1].Y) > ra + rb) return false;
        }
        // L = A2 × B2
        {
            float ra = a.X * absR[1].Z + a.Y * absR[0].Z;
            float rb = b.X * absR[2].Y + b.Y * absR[2].X;
            if (MathF.Abs(tA.Y * R[0].Z - tA.X * R[1].Z) > ra + rb) return false;
        }

        return true;
    }

    private static float Component(Vector3 v, int i) => i switch
    {
        0 => v.X,
        1 => v.Y,
        _ => v.Z,
    };
}

