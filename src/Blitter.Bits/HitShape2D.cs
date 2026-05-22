using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// A delegate that accepts the posed primitives of a <see cref="HitShape2D"/>.
/// </summary>
public delegate void HitShapeVisitor2D(ReadOnlySpan<HitPrimitive2D> primitives);

/// <summary>
/// An abstraction of a 2D collision boundary, 
/// in image-local (bitmap) coordinates (origin at image center, unrotated, unscaled).
/// </summary>
public abstract class HitShape2D
{
    /// <summary>
    /// Local-space bounding circle used for the broad-phase reject.
    /// </summary>
    public abstract BoundingCircle LocalBoundary { get; }

    /// <summary>
    /// Returns true if this shaped, posed by <paramref name="mine"/>, 
    /// hits the <paramref name="other"/> shape, using <paramref name="tester"/>.
    /// </summary>
    public abstract bool TestHit(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester);

    /// <summary>
    /// Returns true if this shaped, posed by <paramref name="mine"/>, 
    /// hits any of the primitives in <paramref name="other"/>, using <paramref name="tester"/>.
    /// </summary>
    public abstract bool TestHitWith(in Pose2D mine, ReadOnlySpan<HitPrimitive2D> other, HitTester2D tester);

    /// <summary>
    /// Calls the <paramref name="visitor"/> with the posed primitives of this shape.
    /// </summary>
    public abstract void Visit(in Pose2D mine, HitShapeVisitor2D visitor);

    /// <summary>
    /// Copies this <see cref="HitShape2D"/> with the center position adjusted.
    /// </summary>
    public abstract HitShape2D Translate(Vector2 offset);

    /// <summary>
    /// Shared "no shape" sentinel — never hits anything.
    /// </summary>
    public static readonly HitShape2D None = new NoneShape();

    private sealed class NoneShape : HitShape2D
    {
        public override BoundingCircle LocalBoundary => BoundingCircle.Empty;
        public override bool TestHit(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester) => false;
        public override bool TestHitWith(in Pose2D mine, ReadOnlySpan<HitPrimitive2D> other, HitTester2D tester) => false;
        public override void Visit(in Pose2D mine, HitShapeVisitor2D visitor) { }
        public override HitShape2D Translate(Vector2 offset) => this;
    }
}

/// <summary>
/// A <see cref="HitShape2D"/> combined with a <see cref="Pose2D"/>
/// (world-space position, rotation, scale, flip).
/// </summary>
public readonly struct PosedHitShape2D
{
    /// <summary>The image-local shape.</summary>
    public readonly HitShape2D Shape;
    /// <summary>World-space pose applied to the local geometry.</summary>
    public readonly Pose2D Pose;

    public PosedHitShape2D(HitShape2D shape, Pose2D pose)
    {
        Shape = shape;
        Pose = pose;
    }

    public PosedHitShape2D(
        HitShape2D shape,
        Vector2 position,
        float rotation = 0f,
        float scale = 1f,
        FlipMode flipped = FlipMode.None)
        : this(shape, new Pose2D(position, rotation, scale, flipped)) { }

    /// <summary>
    /// World-space broad-phase circle, computed from <see cref="HitShape2D.LocalBoundary"/> and the current pose.
    /// </summary>
    public BoundingCircle BoundingCircle
    {
        get
        {
            var local = Shape.LocalBoundary;
            if (local.IsEmpty)
                return new BoundingCircle(Pose.Position, 0f);
            return new BoundingCircle(Pose.Transform(local.Center), local.Radius * Pose.Scale);
        }
    }

    /// <summary>
    /// True when this posed shape hits the <paramref name="other"/>, using <paramref name="tester"/>.
    /// </summary>
    public bool TestHit(in PosedHitShape2D other, HitTester2D tester)
    {
        if (!BoundingCircle.Intersects(other.BoundingCircle))
            return false;
        return Shape.TestHit(in Pose, in other, tester);
    }

    /// <summary>
    /// True when this posed shape hits the <paramref name="other"/>, using the default hit tester.
    /// </summary>
    public bool TestHit(in PosedHitShape2D other) =>
        TestHit(in other, IntersectsHitTester2D.Instance);

    /// <summary>
    /// Hands this shape's current posed primitives to
    /// <paramref name="visitor"/>. Off the collision hot path
    /// (debug rendering, gizmos, tests).
    /// </summary>
    public void Visit(HitShapeVisitor2D visitor) =>
        Shape.Visit(in Pose, visitor);
}

/// <summary>
/// A <see cref="HitShape2D"/> that is a single circle.
/// The center and radius are in image-local coordinates.
/// </summary>
public sealed class CircleHitShape2D : HitShape2D
{
    public Vector2 LocalCenter { get; }
    public float LocalRadius { get; }

    public CircleHitShape2D(Vector2 localCenter, float localRadius)
    {
        LocalCenter = localCenter;
        LocalRadius = localRadius;
    }

    public override BoundingCircle LocalBoundary => new(LocalCenter, LocalRadius);

    public override bool TestHit(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester)
    {
        Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(span, in other);
    }

    public override bool TestHitWith(in Pose2D mine, ReadOnlySpan<HitPrimitive2D> other, HitTester2D tester)
    {
        Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(other, span);
    }

    public override void Visit(in Pose2D mine, HitShapeVisitor2D visitor)
    {
        Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[1];
        span[0] = Pose(in mine);
        visitor(span);
    }

    private HitPrimitive2D Pose(in Pose2D pose) =>
        HitPrimitive2D.Circle(pose.Transform(LocalCenter), LocalRadius * pose.Scale);

    public override HitShape2D Translate(Vector2 offset) =>
        new CircleHitShape2D(LocalCenter + offset, LocalRadius);
}

/// <summary>
/// A <see cref="HitShape2D"/> that is a capsule: the Minkowski sum of
/// segment <see cref="LocalEndA"/>–<see cref="LocalEndB"/> with a disk
/// of <see cref="LocalRadius"/>. Endpoints are in image-local coordinates.
/// </summary>
public sealed class CapsuleHitShape2D : HitShape2D
{
    public Vector2 LocalEndA { get; }
    public Vector2 LocalEndB { get; }
    public float LocalRadius { get; }

    public CapsuleHitShape2D(Vector2 localEndA, Vector2 localEndB, float localRadius)
    {
        LocalEndA = localEndA;
        LocalEndB = localEndB;
        LocalRadius = localRadius;
    }

    public override BoundingCircle LocalBoundary
    {
        get
        {
            var mid = (LocalEndA + LocalEndB) * 0.5f;
            var half = (LocalEndA - LocalEndB).Length() * 0.5f;
            return new BoundingCircle(mid, half + LocalRadius);
        }
    }

    public override bool TestHit(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester)
    {
        Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(span, in other);
    }

    public override bool TestHitWith(in Pose2D mine, ReadOnlySpan<HitPrimitive2D> other, HitTester2D tester)
    {
        Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[1];
        span[0] = Pose(in mine);
        return tester.TestHit(other, span);
    }

    public override void Visit(in Pose2D mine, HitShapeVisitor2D visitor)
    {
        Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[1];
        span[0] = Pose(in mine);
        visitor(span);
    }

    private HitPrimitive2D Pose(in Pose2D pose) =>
        HitPrimitive2D.Capsule(
            pose.Transform(LocalEndA),
            pose.Transform(LocalEndB),
            LocalRadius * pose.Scale);

    public override HitShape2D Translate(Vector2 offset) =>
        new CapsuleHitShape2D(LocalEndA + offset, LocalEndB + offset, LocalRadius);
}
