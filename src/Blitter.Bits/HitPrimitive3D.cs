using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Discriminator for a <see cref="HitPrimitive3D"/>.
/// </summary>
public enum HitKind3D : byte
{
    /// <summary>
    /// A sphere based on a center point and radius.
    /// </summary>
    Sphere,

    /// <summary>
    /// A cylinder with hemispherical end caps.
    /// </summary>
    Capsule,

    /// <summary>
    /// A solid right circular cylinder with flat caps.
    /// </summary>
    Cylinder,

    /// <summary>
    /// A solid oriented box.
    /// </summary>
    Box,

    /// <summary>
    /// An oriented rectangle ("wall").
    /// </summary>
    Wall,

    /// <summary>
    /// A single triangle (three world-space vertices).
    /// </summary>
    Triangle,
}

/// <summary>
/// A single collidable 3D primitive
/// </summary>
public readonly struct HitPrimitive3D
{
    /// <summary>Which primitive shape this struct represents.</summary>
    public readonly HitKind3D Kind;

    /// <summary>Primary point. Center / endpoint A depending on <see cref="Kind"/>.</summary>
    public readonly Vector3 P0;

    /// <summary>
    /// Secondary point. Endpoint B for capsule / cylinder;
    /// half-extents for box / wall. Unused for sphere.
    /// </summary>
    public readonly Vector3 P1;

    /// <summary>Scalar radius. Used by sphere, capsule, cylinder; unused for box / wall.</summary>
    public readonly float R;

    /// <summary>
    /// Orientation. Used by box (rotates the half-extent axes) and
    /// wall (local Z = normal, local X/Y span the rectangle).
    /// <see cref="Quaternion.Identity"/> for all other kinds.
    /// </summary>
    public readonly Quaternion Q;

    private HitPrimitive3D(HitKind3D kind, Vector3 p0, Vector3 p1, float r, Quaternion q)
    {
        Kind = kind;
        P0 = p0;
        P1 = p1;
        R = r;
        Q = q;
    }

    /// <summary>
    /// Converts a <see cref="BoundingSphere"/> to a sphere primitive. 
    /// </summary>
    public static implicit operator HitPrimitive3D(BoundingSphere sphere) =>
        Sphere(sphere.Center, sphere.Radius);

    /// <summary>Builds a sphere primitive.</summary>
    public static HitPrimitive3D Sphere(Vector3 center, float radius) =>
        new(HitKind3D.Sphere, center, default, radius, Quaternion.Identity);

    /// <summary> 
    /// Builds a capsule primitive: a cylinder with spherical caps.
    /// </summary>
    public static HitPrimitive3D Capsule(Vector3 a, Vector3 b, float radius) =>
        new(HitKind3D.Capsule, a, b, radius, Quaternion.Identity);

    /// <summary>
    /// Builds a solid cylinder primitive with flat caps.
    /// </summary>
    public static HitPrimitive3D Cylinder(Vector3 baseCenter, Vector3 topCenter, float radius) =>
        new(HitKind3D.Cylinder, baseCenter, topCenter, radius, Quaternion.Identity);

    /// <summary>
    /// Builds a solid oriented box.
    /// </summary>
    public static HitPrimitive3D Box(Vector3 center, Vector3 halfExtents, Quaternion rotation) =>
        new(HitKind3D.Box, center, halfExtents, 0f, rotation);

    /// <summary>
    /// Builds a two-sided oriented rectangle ("wall"). The rectangle is
    /// the local XY plane spanned by <paramref name="halfExtents"/>;
    /// local Z (after <paramref name="rotation"/>) is the face normal.
    /// </summary>
    public static HitPrimitive3D Wall(Vector3 center, Vector2 halfExtents, Quaternion rotation) =>
        new(HitKind3D.Wall, center, new Vector3(halfExtents, 0f), 0f, rotation);

    /// <summary>
    /// Builds a triangle primitive from three world-space vertices.
    /// Winding determines the outward normal (right-hand rule on
    /// <c>v0 → v1 → v2</c>).
    /// </summary>
    public static HitPrimitive3D Triangle(Vector3 v0, Vector3 v1, Vector3 v2) =>
        // v0/v1 in P0/P1; v2 packed into Q.X/Y/Z (Q.W unused).
        new(HitKind3D.Triangle, v0, v1, 0f, new Quaternion(v2.X, v2.Y, v2.Z, 0f));

    /// <summary>
    /// Reads this primitive as a sphere. The caller is expected to have
    /// dispatched on <see cref="Kind"/>; throws if this primitive is
    /// not a <see cref="HitKind3D.Sphere"/>.
    /// </summary>
    public (Vector3 Center, float Radius) AsSphere()
    {
        if (Kind != HitKind3D.Sphere) throw WrongKind(HitKind3D.Sphere);
        return (P0, R);
    }

    /// <summary>
    /// Reads this primitive as a capsule: a cylinder of
    /// <c>Radius</c> with hemispherical caps centered at
    /// <c>CapA</c> and <c>CapB</c>.
    /// </summary>
    public (Vector3 CapA, Vector3 CapB, float Radius) AsCapsule()
    {
        if (Kind != HitKind3D.Capsule) throw WrongKind(HitKind3D.Capsule);
        return (P0, P1, R);
    }

    /// <summary>
    /// Reads this primitive as a cylinder: a right circular cylinder
    /// with flat caps centered at <c>BaseCenter</c> and <c>TopCenter</c>.
    /// </summary>
    public (Vector3 BaseCenter, Vector3 TopCenter, float Radius) AsCylinder()
    {
        if (Kind != HitKind3D.Cylinder) throw WrongKind(HitKind3D.Cylinder);
        return (P0, P1, R);
    }

    /// <summary>
    /// Reads this primitive as a solid oriented box. <c>HalfExtents</c>
    /// are along the box's local X / Y / Z axes; <c>Rotation</c> turns
    /// those local axes into world space.
    /// </summary>
    public (Vector3 Center, Vector3 HalfExtents, Quaternion Rotation) AsBox()
    {
        if (Kind != HitKind3D.Box) throw WrongKind(HitKind3D.Box);
        return (P0, P1, Q);
    }

    /// <summary>
    /// Reads this primitive as a two-sided oriented rectangle.
    /// <c>HalfExtents</c> span the wall's local X / Y axes;
    /// <c>Rotation</c>'s local Z is the face normal.
    /// </summary>
    public (Vector3 Center, Vector2 HalfExtents, Quaternion Rotation) AsWall()
    {
        if (Kind != HitKind3D.Wall) throw WrongKind(HitKind3D.Wall);
        return (P0, new Vector2(P1.X, P1.Y), Q);
    }

    /// <summary>
    /// Reads this primitive as a triangle of three world-space vertices.
    /// </summary>
    public (Vector3 V0, Vector3 V1, Vector3 V2) AsTriangle()
    {
        if (Kind != HitKind3D.Triangle) throw WrongKind(HitKind3D.Triangle);
        return (P0, P1, new Vector3(Q.X, Q.Y, Q.Z));
    }

    private InvalidOperationException WrongKind(HitKind3D expected) =>
        new($"HitPrimitive3D is a {Kind}, not a {expected}.");

    /// <summary>True when this primitive overlaps <paramref name="other"/>.</summary>
    public bool Intersects(in HitPrimitive3D other)
    {
        return (Kind, other.Kind) switch
        {
            // Sphere row.
            (HitKind3D.Sphere, HitKind3D.Sphere) => SphereSphere(in this, in other),
            (HitKind3D.Sphere, HitKind3D.Capsule) => SphereCapsule(in this, in other),
            (HitKind3D.Sphere, HitKind3D.Cylinder) => SphereCylinder(in this, in other),
            (HitKind3D.Sphere, HitKind3D.Box) => SphereBox(in this, in other),
            (HitKind3D.Sphere, HitKind3D.Wall) => SphereWall(in this, in other),
            (HitKind3D.Sphere, HitKind3D.Triangle) => SphereTriangle(in this, in other),

            // Capsule row (asymmetric pairs reuse the row above).
            (HitKind3D.Capsule, HitKind3D.Sphere) => SphereCapsule(in other, in this),
            (HitKind3D.Capsule, HitKind3D.Capsule) => CapsuleCapsule(in this, in other),
            (HitKind3D.Capsule, HitKind3D.Cylinder) => CapsuleCylinder(in this, in other),
            (HitKind3D.Capsule, HitKind3D.Box) => CapsuleBox(in this, in other),
            (HitKind3D.Capsule, HitKind3D.Wall) => CapsuleWall(in this, in other),
            (HitKind3D.Capsule, HitKind3D.Triangle) => CapsuleTriangle(in this, in other),

            // Cylinder row.
            (HitKind3D.Cylinder, HitKind3D.Sphere) => SphereCylinder(in other, in this),
            (HitKind3D.Cylinder, HitKind3D.Capsule) => CapsuleCylinder(in other, in this),
            (HitKind3D.Cylinder, HitKind3D.Cylinder) => CylinderCylinder(in this, in other),
            (HitKind3D.Cylinder, HitKind3D.Box) => CylinderBox(in this, in other),
            (HitKind3D.Cylinder, HitKind3D.Wall) => CylinderWall(in this, in other),
            (HitKind3D.Cylinder, HitKind3D.Triangle) => CylinderTriangle(in this, in other),

            // Box row.
            (HitKind3D.Box, HitKind3D.Sphere) => SphereBox(in other, in this),
            (HitKind3D.Box, HitKind3D.Capsule) => CapsuleBox(in other, in this),
            (HitKind3D.Box, HitKind3D.Cylinder) => CylinderBox(in other, in this),
            (HitKind3D.Box, HitKind3D.Box) => BoxBox(in this, in other),
            (HitKind3D.Box, HitKind3D.Triangle) => BoxTriangle(in this, in other),

            // Wall row — wall-vs-wall is intentionally not implemented
            // yet (no current use case).
            (HitKind3D.Wall, HitKind3D.Sphere) => SphereWall(in other, in this),
            (HitKind3D.Wall, HitKind3D.Capsule) => CapsuleWall(in other, in this),
            (HitKind3D.Wall, HitKind3D.Cylinder) => CylinderWall(in other, in this),
            (HitKind3D.Wall, HitKind3D.Triangle) => WallTriangle(in this, in other),

            // Triangle row.
            (HitKind3D.Triangle, HitKind3D.Sphere) => SphereTriangle(in other, in this),
            (HitKind3D.Triangle, HitKind3D.Capsule) => CapsuleTriangle(in other, in this),
            (HitKind3D.Triangle, HitKind3D.Cylinder) => CylinderTriangle(in other, in this),
            (HitKind3D.Triangle, HitKind3D.Box) => BoxTriangle(in other, in this),
            (HitKind3D.Triangle, HitKind3D.Wall) => WallTriangle(in other, in this),

            _ => false,
        };
    }

    // ---- Sphere ----

    private static bool SphereSphere(in HitPrimitive3D a, in HitPrimitive3D b)
    {
        var d = a.P0 - b.P0;
        var rs = a.R + b.R;
        return d.LengthSquared() <= rs * rs;
    }

    private static bool SphereCapsule(in HitPrimitive3D sphere, in HitPrimitive3D capsule)
    {
        var distSq = Geometry3D.PointSegmentDistanceSquared(sphere.P0, capsule.P0, capsule.P1);
        var rs = sphere.R + capsule.R;
        return distSq <= rs * rs;
    }

    private static bool SphereCylinder(in HitPrimitive3D sphere, in HitPrimitive3D cyl)
    {
        // Closed form: split the sphere-to-cylinder distance into the
        // axial overflow (past either cap) and radial overflow (past
        // the side). Both zero means the center is inside → hit.
        var axis = cyl.P1 - cyl.P0;
        var axisLenSq = axis.LengthSquared();
        if (axisLenSq <= 1e-12f)
        {
            // Degenerate cylinder collapses to a sphere of radius cyl.R.
            var d2 = (sphere.P0 - cyl.P0).LengthSquared();
            var rs = sphere.R + cyl.R;
            return d2 <= rs * rs;
        }

        var axisLen = MathF.Sqrt(axisLenSq);
        var axisDir = axis / axisLen;
        var v = sphere.P0 - cyl.P0;
        float along = Vector3.Dot(v, axisDir);
        var radial = v - along * axisDir;
        float radialLen = radial.Length();

        float axialOverflow = along < 0f ? -along : (along > axisLen ? along - axisLen : 0f);
        float radialOverflow = radialLen > cyl.R ? radialLen - cyl.R : 0f;
        float distSq = axialOverflow * axialOverflow + radialOverflow * radialOverflow;
        return distSq <= sphere.R * sphere.R;
    }

    private static bool SphereBox(in HitPrimitive3D sphere, in HitPrimitive3D box)
    {
        // Transform the sphere center into the box's local frame, then
        // clamp to ±halfExtents and check distance.
        var inv = Quaternion.Conjugate(box.Q);
        var local = Vector3.Transform(sphere.P0 - box.P0, inv);
        var h = box.P1;
        var clamped = new Vector3(
            Math.Clamp(local.X, -h.X, h.X),
            Math.Clamp(local.Y, -h.Y, h.Y),
            Math.Clamp(local.Z, -h.Z, h.Z));
        return Vector3.DistanceSquared(local, clamped) <= sphere.R * sphere.R;
    }

    private static bool SphereWall(in HitPrimitive3D sphere, in HitPrimitive3D wall)
    {
        // Wall is the local XY rectangle. Transform sphere center to
        // wall-local; clamp to the rectangle in XY; Z is the unclamped
        // normal-direction distance.
        var inv = Quaternion.Conjugate(wall.Q);
        var local = Vector3.Transform(sphere.P0 - wall.P0, inv);
        float hx = wall.P1.X, hy = wall.P1.Y;
        float cx = Math.Clamp(local.X, -hx, hx);
        float cy = Math.Clamp(local.Y, -hy, hy);
        float dx = local.X - cx;
        float dy = local.Y - cy;
        float dz = local.Z;
        return dx * dx + dy * dy + dz * dz <= sphere.R * sphere.R;
    }

    private static bool SphereTriangle(in HitPrimitive3D sphere, in HitPrimitive3D tri)
    {
        var v2 = new Vector3(tri.Q.X, tri.Q.Y, tri.Q.Z);
        var closest = Geometry3D.ClosestPointOnTriangle(sphere.P0, tri.P0, tri.P1, v2);
        return Vector3.DistanceSquared(sphere.P0, closest) <= sphere.R * sphere.R;
    }

    // ---- Capsule ----

    private static bool CapsuleCapsule(in HitPrimitive3D a, in HitPrimitive3D b)
    {
        var distSq = Geometry3D.SegmentSegmentDistanceSquared(a.P0, a.P1, b.P0, b.P1);
        var rs = a.R + b.R;
        return distSq <= rs * rs;
    }

    private static bool CapsuleCylinder(in HitPrimitive3D capsule, in HitPrimitive3D cyl)
    {
        // Approximate the cylinder as a capsule with the same axis and
        // radius. Conservative at the cylinder's flat caps (false
        // positives in a thin rounded-cap band); exact on the body.
        var distSq = Geometry3D.SegmentSegmentDistanceSquared(capsule.P0, capsule.P1, cyl.P0, cyl.P1);
        var rs = capsule.R + cyl.R;
        return distSq <= rs * rs;
    }

    private static bool CapsuleBox(in HitPrimitive3D capsule, in HitPrimitive3D box)
    {
        // Transform the capsule's segment into box-local space, then
        // test against the AABB inflated by the capsule's radius. The
        // inflation produces a box with square corners rather than the
        // true Minkowski sum's rounded corners — slight false positives
        // possible near corners; acceptable for gameplay.
        var inv = Quaternion.Conjugate(box.Q);
        var a = Vector3.Transform(capsule.P0 - box.P0, inv);
        var b = Vector3.Transform(capsule.P1 - box.P0, inv);
        var h = box.P1 + new Vector3(capsule.R);
        return Geometry3D.SegmentIntersectsAabb(a, b, h);
    }

    private static bool CapsuleWall(in HitPrimitive3D capsule, in HitPrimitive3D wall)
    {
        // Same approach as box, with the wall's Z half-extent = 0.
        var inv = Quaternion.Conjugate(wall.Q);
        var a = Vector3.Transform(capsule.P0 - wall.P0, inv);
        var b = Vector3.Transform(capsule.P1 - wall.P0, inv);
        var h = new Vector3(wall.P1.X + capsule.R, wall.P1.Y + capsule.R, capsule.R);
        return Geometry3D.SegmentIntersectsAabb(a, b, h);
    }

    // ---- Cylinder ----

    private static bool CylinderCylinder(in HitPrimitive3D a, in HitPrimitive3D b)
    {
        // Same approximation as capsule-cylinder: treat both axes as
        // capsule segments.
        var distSq = Geometry3D.SegmentSegmentDistanceSquared(a.P0, a.P1, b.P0, b.P1);
        var rs = a.R + b.R;
        return distSq <= rs * rs;
    }

    private static bool CylinderBox(in HitPrimitive3D cyl, in HitPrimitive3D box)
    {
        // Treat the cylinder as a capsule of the same radius. Slight
        // overlap reported at the cylinder's flat caps.
        var inv = Quaternion.Conjugate(box.Q);
        var a = Vector3.Transform(cyl.P0 - box.P0, inv);
        var b = Vector3.Transform(cyl.P1 - box.P0, inv);
        var h = box.P1 + new Vector3(cyl.R);
        return Geometry3D.SegmentIntersectsAabb(a, b, h);
    }

    private static bool CylinderWall(in HitPrimitive3D cyl, in HitPrimitive3D wall)
    {
        var inv = Quaternion.Conjugate(wall.Q);
        var a = Vector3.Transform(cyl.P0 - wall.P0, inv);
        var b = Vector3.Transform(cyl.P1 - wall.P0, inv);
        var h = new Vector3(wall.P1.X + cyl.R, wall.P1.Y + cyl.R, cyl.R);
        return Geometry3D.SegmentIntersectsAabb(a, b, h);
    }

    // ---- Box ----

    private static bool BoxBox(in HitPrimitive3D a, in HitPrimitive3D b) =>
        Geometry3D.BoxesOverlap(a.P0, a.Q, a.P1, b.P0, b.Q, b.P1);

    // ---- Triangle (segment / box / wall pairs) ----

    private static bool CapsuleTriangle(in HitPrimitive3D capsule, in HitPrimitive3D tri)
    {
        var v2 = new Vector3(tri.Q.X, tri.Q.Y, tri.Q.Z);
        float distSq = Geometry3D.SegmentTriangleClosestPoints(
            capsule.P0, capsule.P1, tri.P0, tri.P1, v2,
            out _, out _);
        return distSq <= capsule.R * capsule.R;
    }

    private static bool CylinderTriangle(in HitPrimitive3D cyl, in HitPrimitive3D tri)
    {
        // Capsule approximation (slight false positives near the flat
        // caps), matching the rest of the cylinder pair handlers.
        var v2 = new Vector3(tri.Q.X, tri.Q.Y, tri.Q.Z);
        float distSq = Geometry3D.SegmentTriangleClosestPoints(
            cyl.P0, cyl.P1, tri.P0, tri.P1, v2,
            out _, out _);
        return distSq <= cyl.R * cyl.R;
    }

    private static bool BoxTriangle(in HitPrimitive3D box, in HitPrimitive3D tri)
    {
        var v2 = new Vector3(tri.Q.X, tri.Q.Y, tri.Q.Z);
        return Geometry3D.BoxIntersectsTriangle(
            box.P0, box.Q, box.P1, tri.P0, tri.P1, v2);
    }

    private static bool WallTriangle(in HitPrimitive3D wall, in HitPrimitive3D tri)
    {
        // Wall = degenerate box with Z half-extent 0.
        var v2 = new Vector3(tri.Q.X, tri.Q.Y, tri.Q.Z);
        var h = new Vector3(wall.P1.X, wall.P1.Y, 0f);
        return Geometry3D.BoxIntersectsTriangle(
            wall.P0, wall.Q, h, tri.P0, tri.P1, v2);
    }

    // ---- TryGetContact pair table -------------------------------------
    //
    // Closed-form contact for primitive pairs. Convention: for
    // <c>a.TryGetContact(b, out c)</c>, <c>c.Normal</c> points from
    // <c>b</c> toward <c>a</c>. Asymmetric pairs reuse the canonical
    // direction and flip the result.
    //
    // Currently implemented: every Sphere row pair, plus every
    // primitive against Triangle. Remaining pairs (Capsule-Box,
    // Capsule-Wall, Cylinder-Box, Cylinder-Wall, Box-Box, etc.) return
    // false from <see cref="TryGetContact"/> for now; they can be added
    // as the need arises without breaking callers.

    /// <summary>
    /// Computes a closed-form contact between this primitive and
    /// <paramref name="other"/>. Returns <see langword="false"/> if the
    /// primitives don't overlap or the pair has no contact resolution
    /// yet. <paramref name="contact"/>'s normal points from
    /// <paramref name="other"/> toward this primitive.
    /// </summary>
    public bool TryGetContact(in HitPrimitive3D other, out HitContact3D contact)
    {
        switch (Kind, other.Kind)
        {
            case (HitKind3D.Sphere, HitKind3D.Sphere):
                return SphereSphereContact(in this, in other, out contact);
            case (HitKind3D.Sphere, HitKind3D.Box):
                return SphereBoxContact(in this, in other, out contact);
            case (HitKind3D.Sphere, HitKind3D.Wall):
                return SphereWallContact(in this, in other, out contact);
            case (HitKind3D.Sphere, HitKind3D.Capsule):
                return SphereCapsuleContact(in this, in other, out contact);
            case (HitKind3D.Sphere, HitKind3D.Cylinder):
                return SphereCylinderContact(in this, in other, out contact);
            case (HitKind3D.Sphere, HitKind3D.Triangle):
                return SphereTriangleContact(in this, in other, out contact);
            case (HitKind3D.Capsule, HitKind3D.Triangle):
                return CapsuleTriangleContact(in this, in other, out contact);
            case (HitKind3D.Cylinder, HitKind3D.Triangle):
                return CylinderTriangleContact(in this, in other, out contact);
            case (HitKind3D.Box, HitKind3D.Triangle):
                return BoxTriangleContact(in this, in other, out contact);
            case (HitKind3D.Wall, HitKind3D.Triangle):
                return WallTriangleContact(in this, in other, out contact);

            // Reverse direction: swap args, then flip the normal.
            case (HitKind3D.Box, HitKind3D.Sphere):
            {
                bool hit = SphereBoxContact(in other, in this, out contact);
                if (hit) contact = contact.Flipped();
                return hit;
            }
            case (HitKind3D.Wall, HitKind3D.Sphere):
            {
                bool hit = SphereWallContact(in other, in this, out contact);
                if (hit) contact = contact.Flipped();
                return hit;
            }
            case (HitKind3D.Capsule, HitKind3D.Sphere):
            {
                bool hit = SphereCapsuleContact(in other, in this, out contact);
                if (hit) contact = contact.Flipped();
                return hit;
            }
            case (HitKind3D.Cylinder, HitKind3D.Sphere):
            {
                bool hit = SphereCylinderContact(in other, in this, out contact);
                if (hit) contact = contact.Flipped();
                return hit;
            }
            case (HitKind3D.Triangle, HitKind3D.Sphere):
            {
                bool hit = SphereTriangleContact(in other, in this, out contact);
                if (hit) contact = contact.Flipped();
                return hit;
            }
            case (HitKind3D.Triangle, HitKind3D.Capsule):
            {
                bool hit = CapsuleTriangleContact(in other, in this, out contact);
                if (hit) contact = contact.Flipped();
                return hit;
            }
            case (HitKind3D.Triangle, HitKind3D.Cylinder):
            {
                bool hit = CylinderTriangleContact(in other, in this, out contact);
                if (hit) contact = contact.Flipped();
                return hit;
            }
            case (HitKind3D.Triangle, HitKind3D.Box):
            {
                bool hit = BoxTriangleContact(in other, in this, out contact);
                if (hit) contact = contact.Flipped();
                return hit;
            }
            case (HitKind3D.Triangle, HitKind3D.Wall):
            {
                bool hit = WallTriangleContact(in other, in this, out contact);
                if (hit) contact = contact.Flipped();
                return hit;
            }

            default:
                contact = default;
                return false;
        }
    }

    private static bool SphereSphereContact(in HitPrimitive3D sphere, in HitPrimitive3D b, out HitContact3D contact)
    {
        var d = sphere.P0 - b.P0;
        float distSq = d.LengthSquared();
        float rs = sphere.R + b.R;
        if (distSq > rs * rs)
        {
            contact = default;
            return false;
        }
        float dist = MathF.Sqrt(distSq);
        Vector3 normal = dist > 1e-6f ? d / dist : Vector3.UnitY;
        float pen = rs - dist;
        Vector3 point = b.P0 + normal * b.R;
        contact = new HitContact3D(normal, point, pen);
        return true;
    }

    private static bool SphereBoxContact(in HitPrimitive3D sphere, in HitPrimitive3D box, out HitContact3D contact)
    {
        var inv = Quaternion.Conjugate(box.Q);
        var local = Vector3.Transform(sphere.P0 - box.P0, inv);
        var h = box.P1;
        var clamped = new Vector3(
            Math.Clamp(local.X, -h.X, h.X),
            Math.Clamp(local.Y, -h.Y, h.Y),
            Math.Clamp(local.Z, -h.Z, h.Z));
        var delta = local - clamped;
        float distSq = delta.LengthSquared();
        if (distSq > sphere.R * sphere.R)
        {
            contact = default;
            return false;
        }
        Vector3 localNormal;
        float pen;
        if (distSq > 1e-12f)
        {
            float dist = MathF.Sqrt(distSq);
            localNormal = delta / dist;
            pen = sphere.R - dist;
        }
        else
        {
            // Sphere center is inside the box: push out through the
            // nearest face (smallest gap to a half-extent).
            float gapX = h.X - MathF.Abs(local.X);
            float gapY = h.Y - MathF.Abs(local.Y);
            float gapZ = h.Z - MathF.Abs(local.Z);
            if (gapX <= gapY && gapX <= gapZ)
            {
                float sx = MathF.Sign(local.X);
                localNormal = new Vector3(sx == 0f ? 1f : sx, 0f, 0f);
                pen = sphere.R + gapX;
            }
            else if (gapY <= gapZ)
            {
                float sy = MathF.Sign(local.Y);
                localNormal = new Vector3(0f, sy == 0f ? 1f : sy, 0f);
                pen = sphere.R + gapY;
            }
            else
            {
                float sz = MathF.Sign(local.Z);
                localNormal = new Vector3(0f, 0f, sz == 0f ? 1f : sz);
                pen = sphere.R + gapZ;
            }
        }
        Vector3 normal = Vector3.Transform(localNormal, box.Q);
        Vector3 point = box.P0 + Vector3.Transform(clamped, box.Q);
        contact = new HitContact3D(normal, point, pen);
        return true;
    }

    private static bool SphereWallContact(in HitPrimitive3D sphere, in HitPrimitive3D wall, out HitContact3D contact)
    {
        var inv = Quaternion.Conjugate(wall.Q);
        var local = Vector3.Transform(sphere.P0 - wall.P0, inv);
        float hx = wall.P1.X, hy = wall.P1.Y;
        float cx = Math.Clamp(local.X, -hx, hx);
        float cy = Math.Clamp(local.Y, -hy, hy);
        var clamped = new Vector3(cx, cy, 0f);
        var delta = local - clamped;
        float distSq = delta.LengthSquared();
        if (distSq > sphere.R * sphere.R)
        {
            contact = default;
            return false;
        }
        Vector3 localNormal;
        float pen;
        if (distSq > 1e-12f)
        {
            float dist = MathF.Sqrt(distSq);
            localNormal = delta / dist;
            pen = sphere.R - dist;
        }
        else
        {
            // Sphere center sits exactly on the wall plane within the
            // rectangle: pick the +Z side by convention.
            localNormal = Vector3.UnitZ;
            pen = sphere.R;
        }
        Vector3 normal = Vector3.Transform(localNormal, wall.Q);
        Vector3 point = wall.P0 + Vector3.Transform(clamped, wall.Q);
        contact = new HitContact3D(normal, point, pen);
        return true;
    }

    private static bool SphereCapsuleContact(in HitPrimitive3D sphere, in HitPrimitive3D capsule, out HitContact3D contact)
    {
        var closest = ClosestPointOnSegment(sphere.P0, capsule.P0, capsule.P1);
        var d = sphere.P0 - closest;
        float distSq = d.LengthSquared();
        float rs = sphere.R + capsule.R;
        if (distSq > rs * rs)
        {
            contact = default;
            return false;
        }
        float dist = MathF.Sqrt(distSq);
        Vector3 normal = dist > 1e-6f ? d / dist : Vector3.UnitY;
        float pen = rs - dist;
        Vector3 point = closest + normal * capsule.R;
        contact = new HitContact3D(normal, point, pen);
        return true;
    }

    private static bool SphereCylinderContact(in HitPrimitive3D sphere, in HitPrimitive3D cyl, out HitContact3D contact)
    {
        // Approximate the cylinder as a capsule of the same axis and
        // radius — same approximation as <see cref="Intersects"/>.
        // Slightly rounded contact near the flat caps; exact on the body.
        return SphereCapsuleContact(in sphere, in cyl, out contact);
    }

    private static Vector3 SafeFaceNormal(in Vector3 v0, in Vector3 v1, in Vector3 v2)
    {
        var face = Vector3.Cross(v1 - v0, v2 - v0);
        float fl = face.LengthSquared();
        return fl > 1e-12f ? face / MathF.Sqrt(fl) : Vector3.UnitY;
    }

    // True when `delta` is (approximately) parallel to the triangle's
    // face normal, i.e. the closest-tri-point came from the
    // perpendicular projection of the query point onto the face — not
    // from an edge/vertex clamp. In that case the contact is a face
    // contact and should be resolved one-sidedly along faceNormal.
    private static bool IsFaceAligned(in Vector3 delta, in Vector3 faceNormal)
    {
        float deltaLenSq = delta.LengthSquared();
        // Delta exactly zero (query point on the plane) counts as a
        // face contact; the caller will use faceNormal directly.
        if (deltaLenSq < 1e-12f) return true;
        float crossLenSq = Vector3.Cross(delta, faceNormal).LengthSquared();
        // |a × b|² <= ε² · |a|² · |b|² with |b|=1 → angle below ~tiny.
        return crossLenSq <= 1e-8f * deltaLenSq;
    }

    private static bool SphereTriangleContact(in HitPrimitive3D sphere, in HitPrimitive3D tri, out HitContact3D contact)
    {
        var v0 = tri.P0;
        var v1 = tri.P1;
        var v2 = new Vector3(tri.Q.X, tri.Q.Y, tri.Q.Z);
        var faceNormal = SafeFaceNormal(v0, v1, v2);

        var closest = Geometry3D.ClosestPointOnTriangle(sphere.P0, v0, v1, v2);
        var delta = sphere.P0 - closest;
        float distSq = delta.LengthSquared();

        // Face contact: closest-tri-point is the perpendicular
        // projection of the sphere center onto the triangle's plane
        // and lies inside the triangle. Treat the triangle as
        // one-sided so the sphere is always pushed along +faceNormal,
        // and use the signed plane distance so deep penetrations from
        // the "back" side report the correct depth (R + |d|, not
        // R - |d|).
        if (IsFaceAligned(delta, faceNormal))
        {
            float signedDist = Vector3.Dot(sphere.P0 - v0, faceNormal);
            float pen = sphere.R - signedDist;
            if (pen <= 0f)
            {
                contact = default;
                return false;
            }
            contact = new HitContact3D(faceNormal, closest, pen);
            return true;
        }

        if (distSq > sphere.R * sphere.R)
        {
            contact = default;
            return false;
        }
        float dist = MathF.Sqrt(distSq);
        Vector3 normal = dist > 1e-6f ? delta / dist : faceNormal;
        contact = new HitContact3D(normal, closest, sphere.R - dist);
        return true;
    }

    private static bool CapsuleTriangleContact(in HitPrimitive3D capsule, in HitPrimitive3D tri, out HitContact3D contact)
    {
        var v0 = tri.P0;
        var v1 = tri.P1;
        var v2 = new Vector3(tri.Q.X, tri.Q.Y, tri.Q.Z);
        var faceNormal = SafeFaceNormal(v0, v1, v2);

        float distSq = Geometry3D.SegmentTriangleClosestPoints(
            capsule.P0, capsule.P1, v0, v1, v2,
            out var cs, out var ct);
        var delta = cs - ct;

        // Face contact: the closest segment point projects
        // perpendicularly onto the triangle's interior. Treat the
        // triangle as one-sided (see SphereTriangleContact for the
        // rationale).
        if (IsFaceAligned(delta, faceNormal))
        {
            float signedDist = Vector3.Dot(cs - v0, faceNormal);
            // SegmentTriangleClosestPoints collapses a piercing
            // segment to its plane-intersection point (distSq≈0,
            // signedDist≈0), losing depth information. Recover the
            // true depth from whichever endpoint sits further on the
            // -faceNormal side.
            if (distSq < 1e-10f)
            {
                float sd0 = Vector3.Dot(capsule.P0 - v0, faceNormal);
                float sd1 = Vector3.Dot(capsule.P1 - v0, faceNormal);
                signedDist = MathF.Min(sd0, sd1);
            }
            float pen = capsule.R - signedDist;
            if (pen <= 0f)
            {
                contact = default;
                return false;
            }
            contact = new HitContact3D(faceNormal, ct, pen);
            return true;
        }

        if (distSq > capsule.R * capsule.R)
        {
            contact = default;
            return false;
        }
        float dist = MathF.Sqrt(distSq);
        Vector3 normal = dist > 1e-6f ? delta / dist : faceNormal;
        contact = new HitContact3D(normal, ct, capsule.R - dist);
        return true;
    }

    private static bool CylinderTriangleContact(in HitPrimitive3D cyl, in HitPrimitive3D tri, out HitContact3D contact)
    {
        // Capsule approximation: reuse the capsule path.
        return CapsuleTriangleContact(in cyl, in tri, out contact);
    }

    private static bool BoxTriangleContact(in HitPrimitive3D box, in HitPrimitive3D tri, out HitContact3D contact)
    {
        var v2 = new Vector3(tri.Q.X, tri.Q.Y, tri.Q.Z);
        if (!Geometry3D.BoxTriangleContact(
                box.P0, box.Q, box.P1, tri.P0, tri.P1, v2,
                out var normal, out var point, out var pen))
        {
            contact = default;
            return false;
        }
        contact = new HitContact3D(normal, point, pen);
        return true;
    }

    private static bool WallTriangleContact(in HitPrimitive3D wall, in HitPrimitive3D tri, out HitContact3D contact)
    {
        var v2 = new Vector3(tri.Q.X, tri.Q.Y, tri.Q.Z);
        var h = new Vector3(wall.P1.X, wall.P1.Y, 0f);
        if (!Geometry3D.BoxTriangleContact(
                wall.P0, wall.Q, h, tri.P0, tri.P1, v2,
                out var normal, out var point, out var pen))
        {
            contact = default;
            return false;
        }
        contact = new HitContact3D(normal, point, pen);
        return true;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        float lenSq = ab.LengthSquared();
        if (lenSq <= 1e-12f)
            return a;
        float t = Math.Clamp(Vector3.Dot(p - a, ab) / lenSq, 0f, 1f);
        return a + t * ab;
    }
}

