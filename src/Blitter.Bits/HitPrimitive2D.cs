using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Discriminator for a <see cref="HitPrimitive2D"/>.
/// </summary>
public enum HitKind2D : byte
{
    /// <summary>
    /// Center + radius circle (<see cref="HitPrimitive2D.P0"/> = center,
    /// <see cref="HitPrimitive2D.R"/> = radius).
    /// </summary>
    Circle,

    /// <summary>
    /// Rounded line segment (<see cref="HitPrimitive2D.P0"/> and
    /// <see cref="HitPrimitive2D.P1"/> = endpoints,
    /// <see cref="HitPrimitive2D.R"/> = radius).
    /// </summary>
    Capsule,

    /// <summary>
    /// Solid oriented box
    /// (<see cref="HitPrimitive2D.P0"/> = center,
    /// <see cref="HitPrimitive2D.P1"/> = half-extents along the box's
    /// local X / Y axes, <see cref="HitPrimitive2D.Rotation"/> =
    /// angle of those axes in radians).
    /// </summary>
    Box,
}

/// <summary>
/// A single collidable 2D primitive. Stack-only by convention — built
/// by a <see cref="HitShape2D"/> into a <see cref="System.Span{T}"/>
/// and handed to a <see cref="HitTester2D"/>; never stored on the heap.
/// </summary>
public readonly struct HitPrimitive2D
{
    /// <summary>
    /// Which primitive shape this struct represents.
    /// </summary>
    public readonly HitKind2D Kind;

    /// <summary>
    /// Primary point. Circle center / capsule endpoint A.
    /// </summary>
    public readonly Vector2 P0;

    /// <summary>
    /// Secondary point. Capsule endpoint B; unused for
    /// <see cref="HitKind2D.Circle"/>.
    /// </summary>
    public readonly Vector2 P1;

    /// <summary>
    /// Scalar. Circle radius / capsule radius.
    /// </summary>
    public readonly float R;

    /// <summary>
    /// Rotation angle, in radians. Used by <see cref="HitKind2D.Box"/>;
    /// zero for all other kinds.
    /// </summary>
    public readonly float Rotation;

    private HitPrimitive2D(HitKind2D kind, Vector2 p0, Vector2 p1, float r, float rotation)
    {
        Kind = kind;
        P0 = p0;
        P1 = p1;
        R = r;
        Rotation = rotation;
    }

    /// <summary>
    /// Builds a circle primitive centered on <paramref name="center"/>
    /// with radius <paramref name="radius"/>.
    /// </summary>
    public static HitPrimitive2D Circle(Vector2 center, float radius) =>
        new(HitKind2D.Circle, center, default, radius, 0f);

    /// <summary>
    /// Builds a capsule primitive — the Minkowski sum of the segment
    /// <paramref name="a"/>–<paramref name="b"/> with a disk of
    /// <paramref name="radius"/>. Degenerate endpoints
    /// (<paramref name="a"/> = <paramref name="b"/>) collide as a circle.
    /// A capsule with <paramref name="radius"/> = 0 is a bare line
    /// segment.
    /// </summary>
    public static HitPrimitive2D Capsule(Vector2 a, Vector2 b, float radius) =>
        new(HitKind2D.Capsule, a, b, radius, 0f);

    /// <summary>
    /// Builds a solid oriented box. <paramref name="halfExtents"/> are
    /// the half-widths along the box's local X / Y axes;
    /// <paramref name="rotation"/> (in radians) turns those axes into
    /// world space.
    /// </summary>
    public static HitPrimitive2D Box(Vector2 center, Vector2 halfExtents, float rotation) =>
        new(HitKind2D.Box, center, halfExtents, 0f, rotation);

    /// <summary>
    /// Reads this primitive as a circle. The caller is expected to
    /// have dispatched on <see cref="Kind"/>; throws if this primitive
    /// is not a <see cref="HitKind2D.Circle"/>.
    /// </summary>
    public (Vector2 Center, float Radius) AsCircle()
    {
        if (Kind != HitKind2D.Circle) throw WrongKind(HitKind2D.Circle);
        return (P0, R);
    }

    /// <summary>
    /// Reads this primitive as a capsule: a rectangle of width
    /// <c>2 * Radius</c> with semicircular caps centered at
    /// <c>CapA</c> and <c>CapB</c>.
    /// </summary>
    public (Vector2 CapA, Vector2 CapB, float Radius) AsCapsule()
    {
        if (Kind != HitKind2D.Capsule) throw WrongKind(HitKind2D.Capsule);
        return (P0, P1, R);
    }

    /// <summary>
    /// Reads this primitive as a solid oriented box. <c>HalfExtents</c>
    /// are along the box's local X / Y axes; <c>Rotation</c> (radians)
    /// turns those local axes into world space.
    /// </summary>
    public (Vector2 Center, Vector2 HalfExtents, float Rotation) AsBox()
    {
        if (Kind != HitKind2D.Box) throw WrongKind(HitKind2D.Box);
        return (P0, P1, Rotation);
    }

    private InvalidOperationException WrongKind(HitKind2D expected) =>
        new($"HitPrimitive2D is a {Kind}, not a {expected}.");

    /// <summary>
    /// True when this primitive overlaps <paramref name="other"/>.
    /// </summary>
    public bool Intersects(in HitPrimitive2D other)
    {
        // Pair table. Each (kind, kind) case dispatches to a closed-form
        // test. Asymmetric pairs delegate to the reverse case so each
        // combination is implemented exactly once.
        return (Kind, other.Kind) switch
        {
            (HitKind2D.Circle, HitKind2D.Circle) => IntersectsCircleCircle(in this, in other),
            (HitKind2D.Circle, HitKind2D.Capsule) => IntersectsCircleCapsule(in this, in other),
            (HitKind2D.Circle, HitKind2D.Box) => IntersectsCircleBox(in this, in other),
            (HitKind2D.Capsule, HitKind2D.Circle) => IntersectsCircleCapsule(in other, in this),
            (HitKind2D.Capsule, HitKind2D.Capsule) => IntersectsCapsuleCapsule(in this, in other),
            (HitKind2D.Capsule, HitKind2D.Box) => IntersectsCapsuleBox(in this, in other),
            (HitKind2D.Box, HitKind2D.Circle) => IntersectsCircleBox(in other, in this),
            (HitKind2D.Box, HitKind2D.Capsule) => IntersectsCapsuleBox(in other, in this),
            (HitKind2D.Box, HitKind2D.Box) => IntersectsBoxBox(in this, in other),
            _ => false,
        };
    }

    private static bool IntersectsCircleCircle(in HitPrimitive2D a, in HitPrimitive2D b)
    {
        var d = a.P0 - b.P0;
        var rs = a.R + b.R;
        return d.LengthSquared() <= rs * rs;
    }

    private static bool IntersectsCircleCapsule(in HitPrimitive2D circle, in HitPrimitive2D capsule)
    {
        var distSq = PointSegmentDistanceSquared(circle.P0, capsule.P0, capsule.P1);
        var rs = circle.R + capsule.R;
        return distSq <= rs * rs;
    }

    private static bool IntersectsCapsuleCapsule(in HitPrimitive2D a, in HitPrimitive2D b)
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

    private static bool IntersectsCircleBox(in HitPrimitive2D circle, in HitPrimitive2D box)
    {
        // Transform the circle center into the box's local frame, then
        // clamp to ±halfExtents and check distance.
        var local = ToLocal(circle.P0 - box.P0, box.Rotation);
        var h = box.P1;
        var clamped = new Vector2(
            Math.Clamp(local.X, -h.X, h.X),
            Math.Clamp(local.Y, -h.Y, h.Y));
        return Vector2.DistanceSquared(local, clamped) <= circle.R * circle.R;
    }

    private static bool IntersectsCapsuleBox(in HitPrimitive2D capsule, in HitPrimitive2D box)
    {
        // Transform the capsule's segment into box-local space, then
        // test against the AABB inflated by the capsule's radius. The
        // inflation gives a rectangle with square corners rather than
        // the Minkowski sum's rounded corners — minor false positives
        // possible near corners; acceptable for gameplay.
        var a = ToLocal(capsule.P0 - box.P0, box.Rotation);
        var b = ToLocal(capsule.P1 - box.P0, box.Rotation);
        var h = box.P1 + new Vector2(capsule.R);
        return SegmentIntersectsAabb(a, b, h);
    }

    private static bool IntersectsBoxBox(in HitPrimitive2D a, in HitPrimitive2D b)
    {
        // 2D SAT over 4 candidate separating axes: A's local X, A's
        // local Y, B's local X, B's local Y.
        float cosA = MathF.Cos(a.Rotation), sinA = MathF.Sin(a.Rotation);
        float cosB = MathF.Cos(b.Rotation), sinB = MathF.Sin(b.Rotation);
        var ax = new Vector2(cosA, sinA);
        var ay = new Vector2(-sinA, cosA);
        var bx = new Vector2(cosB, sinB);
        var by = new Vector2(-sinB, cosB);
        var t = b.P0 - a.P0;

        return ProjectionsOverlap(t, ax, a.P1, ax, ay, b.P1, bx, by)
            && ProjectionsOverlap(t, ay, a.P1, ax, ay, b.P1, bx, by)
            && ProjectionsOverlap(t, bx, a.P1, ax, ay, b.P1, bx, by)
            && ProjectionsOverlap(t, by, a.P1, ax, ay, b.P1, bx, by);
    }

    private static bool ProjectionsOverlap(
        Vector2 t, Vector2 axis,
        Vector2 hA, Vector2 ax, Vector2 ay,
        Vector2 hB, Vector2 bx, Vector2 by)
    {
        // Radius of an OBB projected onto axis: sum of (half-extent ×
        // |axis · localAxis|) over the box's two local axes.
        float rA = hA.X * MathF.Abs(Vector2.Dot(axis, ax))
                 + hA.Y * MathF.Abs(Vector2.Dot(axis, ay));
        float rB = hB.X * MathF.Abs(Vector2.Dot(axis, bx))
                 + hB.Y * MathF.Abs(Vector2.Dot(axis, by));
        return MathF.Abs(Vector2.Dot(t, axis)) <= rA + rB;
    }

    private static bool SegmentIntersectsAabb(Vector2 a, Vector2 b, Vector2 halfExtents)
    {
        // Liang–Barsky slab test against the origin-centered AABB.
        var d = b - a;
        float tMin = 0f, tMax = 1f;

        if (MathF.Abs(d.X) < 1e-12f)
        {
            if (MathF.Abs(a.X) > halfExtents.X) return false;
        }
        else
        {
            float t1 = (-halfExtents.X - a.X) / d.X;
            float t2 = (+halfExtents.X - a.X) / d.X;
            if (t1 > t2) (t1, t2) = (t2, t1);
            if (t1 > tMin) tMin = t1;
            if (t2 < tMax) tMax = t2;
            if (tMin > tMax) return false;
        }

        if (MathF.Abs(d.Y) < 1e-12f)
        {
            if (MathF.Abs(a.Y) > halfExtents.Y) return false;
        }
        else
        {
            float t1 = (-halfExtents.Y - a.Y) / d.Y;
            float t2 = (+halfExtents.Y - a.Y) / d.Y;
            if (t1 > t2) (t1, t2) = (t2, t1);
            if (t1 > tMin) tMin = t1;
            if (t2 < tMax) tMax = t2;
            if (tMin > tMax) return false;
        }

        return true;
    }

    private static Vector2 ToLocal(Vector2 worldDelta, float rotation)
    {
        // Rotate by -rotation to enter the local frame.
        float c = MathF.Cos(rotation);
        float s = MathF.Sin(rotation);
        return new Vector2(
            c * worldDelta.X + s * worldDelta.Y,
            -s * worldDelta.X + c * worldDelta.Y);
    }

    // ---- TryGetContact pair table -------------------------------------
    //
    // Closed-form contact for primitive pairs. Convention: for
    // <c>a.TryGetContact(b, out c)</c>, <c>c.Normal</c> points from
    // <c>b</c> toward <c>a</c>. Asymmetric pairs reuse the canonical
    // direction and flip the result.

    /// <summary>
    /// Computes a closed-form contact between this primitive and
    /// <paramref name="other"/>. Returns <see langword="false"/> if the
    /// primitives don't overlap or the pair has no contact resolution
    /// yet. <paramref name="contact"/>'s normal points from
    /// <paramref name="other"/> toward this primitive.
    /// </summary>
    public bool TryGetContact(in HitPrimitive2D other, out HitContact2D contact)
    {
        switch (Kind, other.Kind)
        {
            case (HitKind2D.Circle, HitKind2D.Circle):
                return CircleCircleContact(in this, in other, out contact);
            case (HitKind2D.Circle, HitKind2D.Capsule):
                return CircleCapsuleContact(in this, in other, out contact);
            case (HitKind2D.Circle, HitKind2D.Box):
                return CircleBoxContact(in this, in other, out contact);

            case (HitKind2D.Capsule, HitKind2D.Circle):
            {
                bool hit = CircleCapsuleContact(in other, in this, out contact);
                if (hit) contact = contact.Flipped();
                return hit;
            }
            case (HitKind2D.Box, HitKind2D.Circle):
            {
                bool hit = CircleBoxContact(in other, in this, out contact);
                if (hit) contact = contact.Flipped();
                return hit;
            }

            default:
                contact = default;
                return false;
        }
    }

    private static bool CircleCircleContact(in HitPrimitive2D a, in HitPrimitive2D b, out HitContact2D contact)
    {
        var d = a.P0 - b.P0;
        float distSq = d.LengthSquared();
        float rs = a.R + b.R;
        if (distSq > rs * rs)
        {
            contact = default;
            return false;
        }
        float dist = MathF.Sqrt(distSq);
        Vector2 normal = dist > 1e-6f ? d / dist : new Vector2(0f, -1f);
        float pen = rs - dist;
        Vector2 point = b.P0 + normal * b.R;
        contact = new HitContact2D(normal, point, pen);
        return true;
    }

    private static bool CircleCapsuleContact(in HitPrimitive2D circle, in HitPrimitive2D capsule, out HitContact2D contact)
    {
        var closest = ClosestPointOnSegment(circle.P0, capsule.P0, capsule.P1);
        var d = circle.P0 - closest;
        float distSq = d.LengthSquared();
        float rs = circle.R + capsule.R;
        if (distSq > rs * rs)
        {
            contact = default;
            return false;
        }
        float dist = MathF.Sqrt(distSq);
        Vector2 normal;
        if (dist > 1e-6f)
        {
            normal = d / dist;
        }
        else
        {
            // Circle center sits exactly on the segment: pick the
            // segment's left perpendicular (in screen-space Y-down,
            // this is "to your left" walking A→B).
            var ab = capsule.P1 - capsule.P0;
            float abLen = ab.Length();
            normal = abLen > 1e-6f ? new Vector2(ab.Y, -ab.X) / abLen : new Vector2(0f, -1f);
        }
        float pen = rs - dist;
        Vector2 point = closest + normal * capsule.R;
        contact = new HitContact2D(normal, point, pen);
        return true;
    }

    private static bool CircleBoxContact(in HitPrimitive2D circle, in HitPrimitive2D box, out HitContact2D contact)
    {
        var local = ToLocal(circle.P0 - box.P0, box.Rotation);
        var h = box.P1;
        var clamped = new Vector2(
            Math.Clamp(local.X, -h.X, h.X),
            Math.Clamp(local.Y, -h.Y, h.Y));
        var delta = local - clamped;
        float distSq = delta.LengthSquared();
        if (distSq > circle.R * circle.R)
        {
            contact = default;
            return false;
        }
        Vector2 localNormal;
        float pen;
        if (distSq > 1e-12f)
        {
            float dist = MathF.Sqrt(distSq);
            localNormal = delta / dist;
            pen = circle.R - dist;
        }
        else
        {
            // Circle center inside the box: push out through the
            // nearest face (smallest gap to a half-extent).
            float gapX = h.X - MathF.Abs(local.X);
            float gapY = h.Y - MathF.Abs(local.Y);
            if (gapX <= gapY)
            {
                float sx = MathF.Sign(local.X);
                localNormal = new Vector2(sx == 0f ? 1f : sx, 0f);
                pen = circle.R + gapX;
            }
            else
            {
                float sy = MathF.Sign(local.Y);
                localNormal = new Vector2(0f, sy == 0f ? 1f : sy);
                pen = circle.R + gapY;
            }
        }
        // Rotate localNormal back to world.
        float cos = MathF.Cos(box.Rotation), sin = MathF.Sin(box.Rotation);
        Vector2 normal = new(cos * localNormal.X - sin * localNormal.Y,
                             sin * localNormal.X + cos * localNormal.Y);
        Vector2 point = box.P0 + new Vector2(cos * clamped.X - sin * clamped.Y,
                                             sin * clamped.X + cos * clamped.Y);
        contact = new HitContact2D(normal, point, pen);
        return true;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float lenSq = ab.LengthSquared();
        if (lenSq <= 1e-12f)
            return a;
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
        return a + t * ab;
    }
}
