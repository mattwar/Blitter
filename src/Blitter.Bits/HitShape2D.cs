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
    // Lazily materialized siblings forming a Klein-4 family under FlipMode XOR.
    // Set in one shot the first time Flipped is called on any family member.
    private HitShape2D? _flipH, _flipV, _flipHV;
    private bool _familyBound;

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
    /// Returns a sibling whose local geometry is mirrored according
    /// to <paramref name="flip"/>. Repeated calls return the same
    /// cached instance; the four flip variants of any shape are
    /// interned together so flipping a sibling never allocates.
    /// </summary>
    public virtual HitShape2D Flipped(FlipMode flip)
    {
        if (flip == FlipMode.None)
            return this;
        if (!_familyBound)
        {
            // Materialize the other three members and cross-wire so any
            // member's _flipX field points at the correct family member.
            // Composition follows the Klein-4 group: state ^ flip.
            var h = CreateFlipped(FlipMode.Horizontal);
            var v = CreateFlipped(FlipMode.Vertical);
            var hv = CreateFlipped(FlipMode.Both);
            BindFamily(this, h, v, hv);
        }
        return flip switch
        {
            FlipMode.Horizontal => _flipH!,
            FlipMode.Vertical => _flipV!,
            FlipMode.Both => _flipHV!,
            _ => throw new ArgumentOutOfRangeException(nameof(flip)),
        };
    }

    /// <summary>
    /// Constructs a new shape whose local geometry is mirrored
    /// according to <paramref name="flip"/>. Implementations should
    /// produce a plain mirrored sibling; <see cref="Flipped"/> handles
    /// caching and family wiring.
    /// </summary>
    protected abstract HitShape2D CreateFlipped(FlipMode flip);

    /// <summary>
    /// Mirrors a local-space point under the given flip mode.
    /// </summary>
    protected static Vector2 Mirror(Vector2 p, FlipMode flip) => flip switch
    {
        FlipMode.Horizontal => new Vector2(-p.X, p.Y),
        FlipMode.Vertical => new Vector2(p.X, -p.Y),
        FlipMode.Both => new Vector2(-p.X, -p.Y),
        _ => p,
    };

    private static void BindFamily(HitShape2D none, HitShape2D h, HitShape2D v, HitShape2D hv)
    {
        none._flipH = h;   none._flipV = v;    none._flipHV = hv;  none._familyBound = true;
        h._flipH = none;   h._flipV = hv;      h._flipHV = v;      h._familyBound = true;
        v._flipH = hv;     v._flipV = none;    v._flipHV = h;      v._familyBound = true;
        hv._flipH = v;     hv._flipV = h;      hv._flipHV = none;  hv._familyBound = true;
    }

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
        public override HitShape2D Flipped(FlipMode flip) => this;
        protected override HitShape2D CreateFlipped(FlipMode flip) => this;
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
        float scale = 1f)
        : this(shape, new Pose2D(position, rotation, scale)) { }

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

    protected override HitShape2D CreateFlipped(FlipMode flip) =>
        new CircleHitShape2D(Mirror(LocalCenter, flip), LocalRadius);
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

    protected override HitShape2D CreateFlipped(FlipMode flip) =>
        new CapsuleHitShape2D(Mirror(LocalEndA, flip), Mirror(LocalEndB, flip), LocalRadius);
}

/// <summary>
/// A <see cref="HitShape2D"/> that is a solid oriented box.
/// <see cref="LocalHalfExtents"/> are the half-widths along the box's
/// local X / Y axes; <see cref="LocalRotation"/> (radians) rotates
/// those axes within the image's frame.
/// </summary>
public sealed class BoxHitShape2D : HitShape2D
{
    public Vector2 LocalCenter { get; }
    public Vector2 LocalHalfExtents { get; }
    public float LocalRotation { get; }

    public BoxHitShape2D(Vector2 localCenter, Vector2 localHalfExtents)
        : this(localCenter, localHalfExtents, 0f) { }

    public BoxHitShape2D(Vector2 localCenter, Vector2 localHalfExtents, float localRotation)
    {
        LocalCenter = localCenter;
        LocalHalfExtents = localHalfExtents;
        LocalRotation = localRotation;
    }

    public override BoundingCircle LocalBoundary =>
        new(LocalCenter, LocalHalfExtents.Length());

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
        HitPrimitive2D.Box(
            pose.Transform(LocalCenter),
            LocalHalfExtents * pose.Scale,
            LocalRotation + pose.Rotation * (MathF.PI / 180f));

    public override HitShape2D Translate(Vector2 offset) =>
        new BoxHitShape2D(LocalCenter + offset, LocalHalfExtents, LocalRotation);

    protected override HitShape2D CreateFlipped(FlipMode flip)
    {
        // Mirror the center; half-extents stay positive; the rotation
        // negates for either single-axis flip and is preserved under a
        // 180° flip (Horizontal + Vertical).
        float r = flip == FlipMode.Both ? LocalRotation : -LocalRotation;
        return new BoxHitShape2D(Mirror(LocalCenter, flip), LocalHalfExtents, r);
    }
}
