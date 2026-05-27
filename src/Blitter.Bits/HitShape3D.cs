using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// A delegate that accepts the posed primitives of a <see cref="HitShape3D"/>.
/// </summary>
public delegate void HitShapeVisitor3D(ReadOnlySpan<HitPrimitive3D> primitives);

/// <summary>
/// An abstraction of a 3D collision boundary, in local (model) coordinates
/// (origin at the model's local origin, unrotated, unscaled). The 3D
/// analog of <see cref="HitShape2D"/>.
/// </summary>
public abstract class HitShape3D
{
    /// <summary>Local-space bounding sphere used for the broad-phase reject.</summary>
    public abstract BoundingSphere LocalBoundary { get; }

    /// <summary>
    /// Returns true if this shape, posed by <paramref name="mine"/>, hits
    /// the <paramref name="other"/> shape, using <paramref name="tester"/>.
    /// </summary>
    public abstract bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester);

    /// <summary>
    /// Returns true if this shape, posed by <paramref name="mine"/>, hits
    /// any of the primitives in <paramref name="other"/>, using <paramref name="tester"/>.
    /// </summary>
    public abstract bool TestHitWith(in Pose3D mine, ReadOnlySpan<HitPrimitive3D> other, HitTester3D tester);

    /// <summary>Calls <paramref name="visitor"/> with the posed primitives of this shape.</summary>
    public abstract void Visit(in Pose3D mine, HitShapeVisitor3D visitor);

    /// <summary>Copies this <see cref="HitShape3D"/> with the center offset by <paramref name="offset"/>.</summary>
    public abstract HitShape3D Translate(Vector3 offset);

    /// <summary>Shared "no shape" sentinel — never hits anything.</summary>
    public static readonly HitShape3D None = new NoneShape();

    private sealed class NoneShape : HitShape3D
    {
        public override BoundingSphere LocalBoundary => BoundingSphere.Empty;
        public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester) => false;
        public override bool TestHitWith(in Pose3D mine, ReadOnlySpan<HitPrimitive3D> other, HitTester3D tester) => false;
        public override void Visit(in Pose3D mine, HitShapeVisitor3D visitor) { }
        public override HitShape3D Translate(Vector3 offset) => this;
    }
}

/// <summary>
/// A <see cref="HitShape3D"/> combined with a <see cref="Pose3D"/>
/// (world-space position, orientation, scale).
/// </summary>
public readonly struct PosedHitShape3D
{
    /// <summary>The local-space shape.</summary>
    public readonly HitShape3D Shape;

    /// <summary>World-space pose applied to the local geometry.</summary>
    public readonly Pose3D Pose;

    public PosedHitShape3D(HitShape3D shape, Pose3D pose)
    {
        Shape = shape;
        Pose = pose;
    }

    public PosedHitShape3D(HitShape3D shape, Vector3 position, Quaternion rotation, float scale = 1f)
        : this(shape, new Pose3D(position, rotation, scale)) { }

    /// <summary>
    /// World-space broad-phase sphere, computed from <see cref="HitShape3D.LocalBoundary"/>
    /// and the current pose.
    /// </summary>
    public BoundingSphere BoundingSphere
    {
        get
        {
            var local = Shape.LocalBoundary;
            if (local.IsEmpty)
                return new BoundingSphere(Pose.Position, 0f);
            return new BoundingSphere(Pose.Transform(local.Center), local.Radius * Pose.Scale);
        }
    }

    /// <summary>True when this posed shape hits <paramref name="other"/>, using <paramref name="tester"/>.</summary>
    public bool TestHit(in PosedHitShape3D other, HitTester3D tester)
    {
        if (!BoundingSphere.Intersects(other.BoundingSphere))
            return false;
        return Shape.TestHit(in Pose, in other, tester);
    }

    /// <summary>True when this posed shape hits <paramref name="other"/>, using the default hit tester.</summary>
    public bool TestHit(in PosedHitShape3D other) =>
        TestHit(in other, IntersectsHitTester3D.Instance);

    /// <summary>Hands this shape's current posed primitives to <paramref name="visitor"/>.</summary>
    public void Visit(HitShapeVisitor3D visitor) =>
        Shape.Visit(in Pose, visitor);
}

/// <summary>
/// A <see cref="HitShape3D"/> that is a single sphere. The center and
/// radius are in local (model) coordinates.
/// </summary>
public sealed class SphereHitShape3D : HitShape3D
{
    public Vector3 LocalCenter { get; }
    public float LocalRadius { get; }

    public SphereHitShape3D(Vector3 localCenter, float localRadius)
    {
        LocalCenter = localCenter;
        LocalRadius = localRadius;
    }

    public override BoundingSphere LocalBoundary => new(LocalCenter, LocalRadius);

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(span, in other);
    }

    public override bool TestHitWith(in Pose3D mine, ReadOnlySpan<HitPrimitive3D> other, HitTester3D tester)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(other, span);
    }

    public override void Visit(in Pose3D mine, HitShapeVisitor3D visitor)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        visitor(span);
    }

    private HitPrimitive3D Pose(in Pose3D pose) =>
        HitPrimitive3D.Sphere(pose.Transform(LocalCenter), LocalRadius * pose.Scale);

    public override HitShape3D Translate(Vector3 offset) =>
        new SphereHitShape3D(LocalCenter + offset, LocalRadius);
}

/// <summary>
/// A <see cref="HitShape3D"/> that is a capsule: the Minkowski sum of
/// segment <see cref="LocalEndA"/>–<see cref="LocalEndB"/> with a ball
/// of <see cref="LocalRadius"/>. Endpoints are in local (model) coordinates.
/// </summary>
public sealed class CapsuleHitShape3D : HitShape3D
{
    public Vector3 LocalEndA { get; }
    public Vector3 LocalEndB { get; }
    public float LocalRadius { get; }

    public CapsuleHitShape3D(Vector3 localEndA, Vector3 localEndB, float localRadius)
    {
        LocalEndA = localEndA;
        LocalEndB = localEndB;
        LocalRadius = localRadius;
    }

    public override BoundingSphere LocalBoundary
    {
        get
        {
            var mid = (LocalEndA + LocalEndB) * 0.5f;
            var half = (LocalEndA - LocalEndB).Length() * 0.5f;
            return new BoundingSphere(mid, half + LocalRadius);
        }
    }

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(span, in other);
    }

    public override bool TestHitWith(in Pose3D mine, ReadOnlySpan<HitPrimitive3D> other, HitTester3D tester)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(other, span);
    }

    public override void Visit(in Pose3D mine, HitShapeVisitor3D visitor)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        visitor(span);
    }

    private HitPrimitive3D Pose(in Pose3D pose) =>
        HitPrimitive3D.Capsule(
            pose.Transform(LocalEndA),
            pose.Transform(LocalEndB),
            LocalRadius * pose.Scale);

    public override HitShape3D Translate(Vector3 offset) =>
        new CapsuleHitShape3D(LocalEndA + offset, LocalEndB + offset, LocalRadius);
}

/// <summary>
/// A <see cref="HitShape3D"/> that is a solid right cylinder with flat
/// caps. The axis runs from <see cref="LocalBase"/> to
/// <see cref="LocalTop"/> in local (model) coordinates.
/// </summary>
public sealed class CylinderHitShape3D : HitShape3D
{
    public Vector3 LocalBase { get; }
    public Vector3 LocalTop { get; }
    public float LocalRadius { get; }

    public CylinderHitShape3D(Vector3 localBase, Vector3 localTop, float localRadius)
    {
        LocalBase = localBase;
        LocalTop = localTop;
        LocalRadius = localRadius;
    }

    public override BoundingSphere LocalBoundary
    {
        get
        {
            var mid = (LocalBase + LocalTop) * 0.5f;
            var half = (LocalTop - LocalBase).Length() * 0.5f;
            return new BoundingSphere(mid, half + LocalRadius);
        }
    }

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(span, in other);
    }

    public override bool TestHitWith(in Pose3D mine, ReadOnlySpan<HitPrimitive3D> other, HitTester3D tester)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(other, span);
    }

    public override void Visit(in Pose3D mine, HitShapeVisitor3D visitor)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        visitor(span);
    }

    private HitPrimitive3D Pose(in Pose3D pose) =>
        HitPrimitive3D.Cylinder(
            pose.Transform(LocalBase),
            pose.Transform(LocalTop),
            LocalRadius * pose.Scale);

    public override HitShape3D Translate(Vector3 offset) =>
        new CylinderHitShape3D(LocalBase + offset, LocalTop + offset, LocalRadius);
}

/// <summary>
/// A <see cref="HitShape3D"/> that is a solid oriented box.
/// <see cref="LocalHalfExtents"/> are the half-widths along the box's
/// local X / Y / Z axes; <see cref="LocalRotation"/> rotates those
/// axes within the model's frame (the pose's rotation rotates the
/// model on top).
/// </summary>
public sealed class BoxHitShape3D : HitShape3D
{
    public Vector3 LocalCenter { get; }
    public Vector3 LocalHalfExtents { get; }
    public Quaternion LocalRotation { get; }

    public BoxHitShape3D(Vector3 localCenter, Vector3 localHalfExtents)
        : this(localCenter, localHalfExtents, Quaternion.Identity) { }

    public BoxHitShape3D(Vector3 localCenter, Vector3 localHalfExtents, Quaternion localRotation)
    {
        LocalCenter = localCenter;
        LocalHalfExtents = localHalfExtents;
        LocalRotation = localRotation;
    }

    public override BoundingSphere LocalBoundary =>
        new(LocalCenter, LocalHalfExtents.Length());

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(span, in other);
    }

    public override bool TestHitWith(in Pose3D mine, ReadOnlySpan<HitPrimitive3D> other, HitTester3D tester)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(other, span);
    }

    public override void Visit(in Pose3D mine, HitShapeVisitor3D visitor)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        visitor(span);
    }

    private HitPrimitive3D Pose(in Pose3D pose) =>
        HitPrimitive3D.Box(
            pose.Transform(LocalCenter),
            LocalHalfExtents * pose.Scale,
            pose.Rotation * LocalRotation);

    public override HitShape3D Translate(Vector3 offset) =>
        new BoxHitShape3D(LocalCenter + offset, LocalHalfExtents, LocalRotation);
}

/// <summary>
/// A <see cref="HitShape3D"/> that is a two-sided oriented rectangle
/// ("wall"). The rectangle is the local XY plane spanned by
/// <see cref="LocalHalfExtents"/>; <see cref="LocalRotation"/>'s local
/// Z axis is the face normal.
/// </summary>
public sealed class WallHitShape3D : HitShape3D
{
    public Vector3 LocalCenter { get; }
    public Vector2 LocalHalfExtents { get; }
    public Quaternion LocalRotation { get; }

    public WallHitShape3D(Vector3 localCenter, Vector2 localHalfExtents)
        : this(localCenter, localHalfExtents, Quaternion.Identity) { }

    public WallHitShape3D(Vector3 localCenter, Vector2 localHalfExtents, Quaternion localRotation)
    {
        LocalCenter = localCenter;
        LocalHalfExtents = localHalfExtents;
        LocalRotation = localRotation;
    }

    public override BoundingSphere LocalBoundary =>
        new(LocalCenter, LocalHalfExtents.Length());

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(span, in other);
    }

    public override bool TestHitWith(in Pose3D mine, ReadOnlySpan<HitPrimitive3D> other, HitTester3D tester)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(other, span);
    }

    public override void Visit(in Pose3D mine, HitShapeVisitor3D visitor)
    {
        Span<HitPrimitive3D> span = stackalloc HitPrimitive3D[1];
        span[0] = Pose(in mine);
        visitor(span);
    }

    private HitPrimitive3D Pose(in Pose3D pose) =>
        HitPrimitive3D.Wall(
            pose.Transform(LocalCenter),
            LocalHalfExtents * pose.Scale,
            pose.Rotation * LocalRotation);

    public override HitShape3D Translate(Vector3 offset) =>
        new WallHitShape3D(LocalCenter + offset, LocalHalfExtents, LocalRotation);
}
