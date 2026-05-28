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
    public static float SegmentSegmentDistanceSquared(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2) =>
        SegmentSegmentClosestPoints(p1, q1, p2, q2, out _, out _);

    /// <summary>
    /// Closest points on two finite segments and the squared distance
    /// between them. <paramref name="c1"/> lies on
    /// <paramref name="p1"/>–<paramref name="q1"/>, <paramref name="c2"/>
    /// on <paramref name="p2"/>–<paramref name="q2"/>. Reference:
    /// Ericson, <i>Real-Time Collision Detection</i>, §5.1.9.
    /// </summary>
    public static float SegmentSegmentClosestPoints(
        Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2,
        out Vector3 c1, out Vector3 c2)
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
        {
            c1 = p1;
            c2 = p2;
            return (p1 - p2).LengthSquared();
        }

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

        c1 = p1 + d1 * s;
        c2 = p2 + d2 * t;
        return (c1 - c2).LengthSquared();
    }

    /// <summary>
    /// True when <paramref name="p"/> lies inside (or on the edge of)
    /// triangle <c>v0–v1–v2</c>. The point is assumed to be in or near
    /// the triangle's plane; the test projects <paramref name="p"/>
    /// onto the plane via barycentric coordinates and accepts the
    /// in-plane case.
    /// </summary>
    public static bool PointInTriangle(Vector3 p, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        var e1 = v1 - v0;
        var e2 = v2 - v0;
        var dp = p - v0;
        float d11 = Vector3.Dot(e1, e1);
        float d12 = Vector3.Dot(e1, e2);
        float d22 = Vector3.Dot(e2, e2);
        float d1p = Vector3.Dot(e1, dp);
        float d2p = Vector3.Dot(e2, dp);
        float denom = d11 * d22 - d12 * d12;
        if (MathF.Abs(denom) <= 1e-12f)
            return false;
        float inv = 1f / denom;
        float u = (d22 * d1p - d12 * d2p) * inv;
        float v = (d11 * d2p - d12 * d1p) * inv;
        const float slack = 1e-5f;
        return u >= -slack && v >= -slack && u + v <= 1f + slack;
    }

    /// <summary>
    /// Closest points between a finite segment <c>a–b</c> and a
    /// triangle <c>v0–v1–v2</c>, with their squared distance.
    /// <paramref name="cs"/> lies on the segment;
    /// <paramref name="ct"/> on the triangle (interior, edge, or
    /// vertex). When the segment pierces the triangle they coincide
    /// and the returned distance is zero.
    /// </summary>
    public static float SegmentTriangleClosestPoints(
        Vector3 a, Vector3 b,
        Vector3 v0, Vector3 v1, Vector3 v2,
        out Vector3 cs, out Vector3 ct)
    {
        // 1. Segment endpoints projected onto the triangle.
        var ta = ClosestPointOnTriangle(a, v0, v1, v2);
        float best = (a - ta).LengthSquared();
        cs = a;
        ct = ta;

        var tb = ClosestPointOnTriangle(b, v0, v1, v2);
        float db = (b - tb).LengthSquared();
        if (db < best)
        {
            best = db;
            cs = b;
            ct = tb;
        }

        // 2. Segment vs each triangle edge.
        TryEdge(a, b, v0, v1, ref best, ref cs, ref ct);
        TryEdge(a, b, v1, v2, ref best, ref cs, ref ct);
        TryEdge(a, b, v2, v0, ref best, ref cs, ref ct);

        // 3. Does the segment actually pierce the triangle interior?
        var normal = Vector3.Cross(v1 - v0, v2 - v0);
        float nLenSq = normal.LengthSquared();
        if (nLenSq > 1e-12f)
        {
            float d0 = Vector3.Dot(normal, a - v0);
            float d1 = Vector3.Dot(normal, b - v0);
            // Strict sign change → an interior crossing exists at
            // parameter t ∈ (0,1). Touch/coplanar cases are handled
            // by the endpoint and edge tests above.
            if ((d0 > 0f && d1 < 0f) || (d0 < 0f && d1 > 0f))
            {
                float t = d0 / (d0 - d1);
                var p = a + t * (b - a);
                if (PointInTriangle(p, v0, v1, v2))
                {
                    cs = p;
                    ct = p;
                    return 0f;
                }
            }
        }

        return best;

        static void TryEdge(
            Vector3 a, Vector3 b, Vector3 e0, Vector3 e1,
            ref float best, ref Vector3 cs, ref Vector3 ct)
        {
            float d = SegmentSegmentClosestPoints(a, b, e0, e1, out var p, out var q);
            if (d < best)
            {
                best = d;
                cs = p;
                ct = q;
            }
        }
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
        {
            absR[i] = new Vector3(
                MathF.Abs(R[i].X) + eps,
                MathF.Abs(R[i].Y) + eps,
                MathF.Abs(R[i].Z) + eps);
        }

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

    /// <summary>
    /// True when the oriented box (world center / rotation / local
    /// half-extents) overlaps triangle <c>v0–v1–v2</c>. Uses the
    /// 13-axis Separating Axis Theorem: 3 box face normals, 1 triangle
    /// face normal, and 9 cross-products of box edges with triangle
    /// edges.
    /// </summary>
    public static bool BoxIntersectsTriangle(
        Vector3 boxCenter, Quaternion boxRotation, Vector3 boxHalfExtents,
        Vector3 v0, Vector3 v1, Vector3 v2)
    {
        ToBoxLocal(boxCenter, boxRotation, v0, v1, v2,
            out var lv0, out var lv1, out var lv2);
        return BoxTriangleSat(boxHalfExtents, lv0, lv1, lv2,
            wantContact: false,
            out _, out _);
    }

    /// <summary>
    /// Computes the contact between an oriented box and a triangle via
    /// SAT, using the axis of minimum penetration. Returns
    /// <see langword="false"/> when the shapes are separated.
    /// <paramref name="normal"/> points from the triangle toward the
    /// box; <paramref name="point"/> is the triangle point closest to
    /// the box center (a stable, useful proxy for the contact location
    /// for the typical "push box off a static triangle" use).
    /// </summary>
    public static bool BoxTriangleContact(
        Vector3 boxCenter, Quaternion boxRotation, Vector3 boxHalfExtents,
        Vector3 v0, Vector3 v1, Vector3 v2,
        out Vector3 normal, out Vector3 point, out float penetration)
    {
        ToBoxLocal(boxCenter, boxRotation, v0, v1, v2,
            out var lv0, out var lv1, out var lv2);

        if (!BoxTriangleSat(boxHalfExtents, lv0, lv1, lv2,
                wantContact: true,
                out var localNormal, out penetration))
        {
            normal = default;
            point = default;
            return false;
        }

        normal = Vector3.Transform(localNormal, boxRotation);

        // Pick a representative contact point: triangle point closest
        // to box center (= local origin). Useful for moving-barrier
        // surface velocity queries and visual debugging.
        var localClosest = ClosestPointOnTriangle(Vector3.Zero, lv0, lv1, lv2);
        point = boxCenter + Vector3.Transform(localClosest, boxRotation);
        return true;
    }

    private static void ToBoxLocal(
        Vector3 boxCenter, Quaternion boxRotation,
        Vector3 v0, Vector3 v1, Vector3 v2,
        out Vector3 lv0, out Vector3 lv1, out Vector3 lv2)
    {
        var inv = Quaternion.Conjugate(boxRotation);
        lv0 = Vector3.Transform(v0 - boxCenter, inv);
        lv1 = Vector3.Transform(v1 - boxCenter, inv);
        lv2 = Vector3.Transform(v2 - boxCenter, inv);
    }

    // SAT body shared by box-vs-triangle intersect and contact. The
    // triangle is given in box-local coordinates (box is the AABB
    // ±halfExtents at the origin). On overlap, returns the local-frame
    // contact normal (pointing from the triangle toward the box) and
    // the penetration depth along that normal.
    private static bool BoxTriangleSat(
        Vector3 halfExtents, Vector3 lv0, Vector3 lv1, Vector3 lv2,
        bool wantContact,
        out Vector3 localNormal, out float penetration)
    {
        localNormal = default;
        penetration = 0f;

        var triNormal = Vector3.Cross(lv1 - lv0, lv2 - lv0);
        // Triangle edges; the three box edge directions are the local
        // unit axes (UnitX/Y/Z) so we compute the nine box-edge ×
        // triangle-edge axes inline below.
        var e0 = lv1 - lv0;
        var e1 = lv2 - lv1;
        var e2 = lv0 - lv2;

        float bestPen = float.PositiveInfinity;
        Vector3 bestAxis = default;

        // 1) box face normals
        if (!TryAxis(Vector3.UnitX, halfExtents, lv0, lv1, lv2, wantContact, ref bestPen, ref bestAxis)) return false;
        if (!TryAxis(Vector3.UnitY, halfExtents, lv0, lv1, lv2, wantContact, ref bestPen, ref bestAxis)) return false;
        if (!TryAxis(Vector3.UnitZ, halfExtents, lv0, lv1, lv2, wantContact, ref bestPen, ref bestAxis)) return false;

        // 2) triangle face normal
        if (!TryAxis(triNormal, halfExtents, lv0, lv1, lv2, wantContact, ref bestPen, ref bestAxis)) return false;

        // 3) nine edge-edge cross products
        if (!TryAxis(Vector3.Cross(Vector3.UnitX, e0), halfExtents, lv0, lv1, lv2, wantContact, ref bestPen, ref bestAxis)) return false;
        if (!TryAxis(Vector3.Cross(Vector3.UnitX, e1), halfExtents, lv0, lv1, lv2, wantContact, ref bestPen, ref bestAxis)) return false;
        if (!TryAxis(Vector3.Cross(Vector3.UnitX, e2), halfExtents, lv0, lv1, lv2, wantContact, ref bestPen, ref bestAxis)) return false;
        if (!TryAxis(Vector3.Cross(Vector3.UnitY, e0), halfExtents, lv0, lv1, lv2, wantContact, ref bestPen, ref bestAxis)) return false;
        if (!TryAxis(Vector3.Cross(Vector3.UnitY, e1), halfExtents, lv0, lv1, lv2, wantContact, ref bestPen, ref bestAxis)) return false;
        if (!TryAxis(Vector3.Cross(Vector3.UnitY, e2), halfExtents, lv0, lv1, lv2, wantContact, ref bestPen, ref bestAxis)) return false;
        if (!TryAxis(Vector3.Cross(Vector3.UnitZ, e0), halfExtents, lv0, lv1, lv2, wantContact, ref bestPen, ref bestAxis)) return false;
        if (!TryAxis(Vector3.Cross(Vector3.UnitZ, e1), halfExtents, lv0, lv1, lv2, wantContact, ref bestPen, ref bestAxis)) return false;
        if (!TryAxis(Vector3.Cross(Vector3.UnitZ, e2), halfExtents, lv0, lv1, lv2, wantContact, ref bestPen, ref bestAxis)) return false;

        if (wantContact)
        {
            // Guard against an all-degenerate-axis case (collinear
            // triangle in a box-aligned configuration); pick the box's
            // smallest-half-extent axis so we still produce a sane
            // push direction.
            if (!float.IsFinite(bestPen))
            {
                bestAxis = Vector3.UnitY;
                bestPen = 0f;
            }
            localNormal = bestAxis;
            penetration = bestPen;
        }
        return true;

        static bool TryAxis(
            Vector3 axis, Vector3 h,
            Vector3 lv0, Vector3 lv1, Vector3 lv2,
            bool wantContact,
            ref float bestPen, ref Vector3 bestAxis)
        {
            float ll = axis.LengthSquared();
            if (ll < 1e-12f)
                // Degenerate axis (parallel edges) → carries no info.
                // Skip rather than treating as separating.
                return true;
            var unit = axis / MathF.Sqrt(ll);

            // Box radius along an arbitrary unit axis is the sum of
            // |axis · faceNormal| * halfExtent over the three faces.
            float rb = MathF.Abs(unit.X) * h.X + MathF.Abs(unit.Y) * h.Y + MathF.Abs(unit.Z) * h.Z;

            float p0 = Vector3.Dot(lv0, unit);
            float p1 = Vector3.Dot(lv1, unit);
            float p2 = Vector3.Dot(lv2, unit);
            float triMin = MathF.Min(p0, MathF.Min(p1, p2));
            float triMax = MathF.Max(p0, MathF.Max(p1, p2));

            if (triMin > rb || triMax < -rb)
                return false;

            if (!wantContact)
                return true;

            // Two escape directions. To push the box in -axis until it
            // clears the triangle, the box's +face must drop to triMin
            // (cost = rb - triMin). To push the box in +axis until it
            // clears, the box's -face must rise to triMax
            // (cost = triMax + rb). Smaller cost wins; the contact
            // normal (b → a, here triangle → box) is the direction the
            // box moves to escape.
            float pushNeg = rb - triMin;
            float pushPos = triMax + rb;
            float pen;
            Vector3 dir;
            if (pushNeg < pushPos)
            {
                pen = pushNeg;
                dir = -unit;
            }
            else
            {
                pen = pushPos;
                dir = unit;
            }
            if (pen < bestPen)
            {
                bestPen = pen;
                bestAxis = dir;
            }
            return true;
        }
    }
}

