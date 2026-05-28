using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// A delegate that receives one posed primitive of a <see cref="HitShape2D"/>.
/// </summary>
public delegate void HitPrimitiveAction2D(in HitPrimitive2D primitive);

/// <summary>
/// An abstraction of a 2D collision boundary, in image-local (bitmap)
/// coordinates (origin at image center, unrotated, unscaled).
/// </summary>
/// <remarks>
/// Hit-testing is callback-driven and allocation-free: each shape walks
/// its own primitives one at a time, delegating each to the other
/// shape's primitive overload or, at the leaves, to a
/// <see cref="HitTester2D"/> that runs the primitive-vs-primitive math.
/// </remarks>
public abstract class HitShape2D
{
    // Lazily materialized siblings forming a Klein-4 family under FlipMode XOR.
    // Set in one shot the first time Flipped is called on any family member.
    private HitShape2D? _flipH, _flipV, _flipHV;
    private bool _familyBound;

    /// <summary>Local-space bounding circle used for the broad-phase reject.</summary>
    public abstract BoundingCircle LocalBoundary { get; }

    /// <summary>
    /// How many primitives this shape emits per hit-test pass.
    /// Compound shapes consult this on the <em>other</em> shape to
    /// decide whether a per-primitive broad-phase prune is worthwhile.
    /// </summary>
    public abstract int PrimitiveCount { get; }

    /// <summary>
    /// True when this shape, posed by <paramref name="mine"/>, hits the
    /// <paramref name="other"/> posed shape.
    /// </summary>
    public abstract bool TestHit(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester);

    /// <summary>
    /// True when this shape, posed by <paramref name="mine"/>, hits the
    /// single primitive <paramref name="otherPrim"/>.
    /// </summary>
    public abstract bool TestHit(in Pose2D mine, in HitPrimitive2D otherPrim, HitTester2D tester);

    /// <summary>
    /// Computes the deepest closed-form contact between this shape
    /// (posed by <paramref name="mine"/>) and <paramref name="other"/>.
    /// Convention: <see cref="HitContact2D.Normal"/> points from
    /// <paramref name="other"/> toward this shape.
    /// </summary>
    public abstract bool TryGetContact(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester, out HitContact2D contact);

    /// <summary>
    /// Computes the deepest closed-form contact between this shape
    /// (posed by <paramref name="mine"/>) and the single primitive
    /// <paramref name="otherPrim"/>. Normal points from
    /// <paramref name="otherPrim"/> toward this shape.
    /// </summary>
    public abstract bool TryGetContact(in Pose2D mine, in HitPrimitive2D otherPrim, HitTester2D tester, out HitContact2D contact);

    /// <summary>Hands every posed primitive of this shape to <paramref name="action"/>, one at a time.</summary>
    public abstract void Visit(in Pose2D mine, HitPrimitiveAction2D action);

    /// <summary>Copies this <see cref="HitShape2D"/> with the center position adjusted.</summary>
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

    /// <summary>Mirrors a local-space point under the given flip mode.</summary>
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

    /// <summary>Shared "no shape" sentinel — never hits anything.</summary>
    public static readonly HitShape2D None = new NoneShape();

    private sealed class NoneShape : HitShape2D
    {
        public override BoundingCircle LocalBoundary => BoundingCircle.Empty;
        public override int PrimitiveCount => 0;
        public override bool TestHit(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester) => false;
        public override bool TestHit(in Pose2D mine, in HitPrimitive2D otherPrim, HitTester2D tester) => false;
        public override bool TryGetContact(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester, out HitContact2D contact)
        {
            contact = default;
            return false;
        }
        public override bool TryGetContact(in Pose2D mine, in HitPrimitive2D otherPrim, HitTester2D tester, out HitContact2D contact)
        {
            contact = default;
            return false;
        }
        public override void Visit(in Pose2D mine, HitPrimitiveAction2D action) { }
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

    /// <summary>True when this posed shape hits <paramref name="other"/> using <see cref="HitTester2D.Default"/>.</summary>
    public bool TestHit(in PosedHitShape2D other) =>
        TestHit(in other, HitTester2D.Default);

    /// <summary>True when this posed shape hits <paramref name="other"/>.</summary>
    public bool TestHit(in PosedHitShape2D other, HitTester2D tester)
    {
        if (!BoundingCircle.Intersects(other.BoundingCircle))
            return false;
        return Shape.TestHit(in Pose, in other, tester);
    }

    /// <summary>
    /// Reports the deepest contact between this posed shape and
    /// <paramref name="other"/> using <see cref="HitTester2D.Default"/>.
    /// </summary>
    public bool TryGetContact(in PosedHitShape2D other, out HitContact2D contact) =>
        TryGetContact(in other, HitTester2D.Default, out contact);

    /// <summary>
    /// Reports the deepest contact between this posed shape and
    /// <paramref name="other"/>. Normal points from
    /// <paramref name="other"/> toward this shape.
    /// </summary>
    public bool TryGetContact(in PosedHitShape2D other, HitTester2D tester, out HitContact2D contact)
    {
        if (!BoundingCircle.Intersects(other.BoundingCircle))
        {
            contact = default;
            return false;
        }
        return Shape.TryGetContact(in Pose, in other, tester, out contact);
    }

    /// <summary>Hands this shape's current posed primitives to <paramref name="action"/>, one at a time.</summary>
    public void Visit(HitPrimitiveAction2D action) =>
        Shape.Visit(in Pose, action);
}

/// <summary>
/// A <see cref="HitShape2D"/> that is a single circle. The center and
/// radius are in image-local coordinates.
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

    public override int PrimitiveCount => 1;

    public override bool TestHit(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester)
    {
        var p = Pose(in mine);
        return other.Shape.TestHit(in other.Pose, in p, tester);
    }

    public override bool TestHit(in Pose2D mine, in HitPrimitive2D otherPrim, HitTester2D tester)
    {
        var p = Pose(in mine);
        return tester.TestHit(in p, in otherPrim);
    }

    public override bool TryGetContact(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester, out HitContact2D contact)
    {
        var p = Pose(in mine);
        if (other.Shape.TryGetContact(in other.Pose, in p, tester, out contact))
        {
            contact = contact.Flipped();
            return true;
        }
        return false;
    }

    public override bool TryGetContact(in Pose2D mine, in HitPrimitive2D otherPrim, HitTester2D tester, out HitContact2D contact)
    {
        var p = Pose(in mine);
        return tester.TryGetContact(in p, in otherPrim, out contact);
    }

    public override void Visit(in Pose2D mine, HitPrimitiveAction2D action) =>
        action(Pose(in mine));

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

    public override int PrimitiveCount => 1;

    public override bool TestHit(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester)
    {
        var p = Pose(in mine);
        return other.Shape.TestHit(in other.Pose, in p, tester);
    }

    public override bool TestHit(in Pose2D mine, in HitPrimitive2D otherPrim, HitTester2D tester)
    {
        var p = Pose(in mine);
        return tester.TestHit(in p, in otherPrim);
    }

    public override bool TryGetContact(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester, out HitContact2D contact)
    {
        var p = Pose(in mine);
        if (other.Shape.TryGetContact(in other.Pose, in p, tester, out contact))
        {
            contact = contact.Flipped();
            return true;
        }
        return false;
    }

    public override bool TryGetContact(in Pose2D mine, in HitPrimitive2D otherPrim, HitTester2D tester, out HitContact2D contact)
    {
        var p = Pose(in mine);
        return tester.TryGetContact(in p, in otherPrim, out contact);
    }

    public override void Visit(in Pose2D mine, HitPrimitiveAction2D action) =>
        action(Pose(in mine));

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

    public override int PrimitiveCount => 1;

    public override bool TestHit(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester)
    {
        var p = Pose(in mine);
        return other.Shape.TestHit(in other.Pose, in p, tester);
    }

    public override bool TestHit(in Pose2D mine, in HitPrimitive2D otherPrim, HitTester2D tester)
    {
        var p = Pose(in mine);
        return tester.TestHit(in p, in otherPrim);
    }

    public override bool TryGetContact(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester, out HitContact2D contact)
    {
        var p = Pose(in mine);
        if (other.Shape.TryGetContact(in other.Pose, in p, tester, out contact))
        {
            contact = contact.Flipped();
            return true;
        }
        return false;
    }

    public override bool TryGetContact(in Pose2D mine, in HitPrimitive2D otherPrim, HitTester2D tester, out HitContact2D contact)
    {
        var p = Pose(in mine);
        return tester.TryGetContact(in p, in otherPrim, out contact);
    }

    public override void Visit(in Pose2D mine, HitPrimitiveAction2D action) =>
        action(Pose(in mine));

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

/// <summary>
/// A <see cref="HitShape2D"/> that is a bare line segment. Posed as a
/// <see cref="HitKind2D.Capsule"/> primitive with radius 0. When
/// <see cref="OneSided"/> is true, only collides with shapes whose
/// representative point lies on the segment's <em>outward</em> side,
/// where outward = <c>perp(B - A)</c> in screen-space Y-down (i.e. to
/// your left walking from A to B). Used by
/// <c>Blitter.Blocks2D.LineBarrier2D</c> to model jump-through floors,
/// one-way gates, and similar one-sided walls.
/// </summary>
public sealed class SegmentHitShape2D : HitShape2D
{
    public Vector2 LocalEndA { get; }
    public Vector2 LocalEndB { get; }

    /// <summary>
    /// When true, only register a hit / contact when the other shape's
    /// representative point is on the outward side of the segment
    /// (left of A→B walking in screen-space Y-down).
    /// </summary>
    public bool OneSided { get; }

    public SegmentHitShape2D(Vector2 localEndA, Vector2 localEndB, bool oneSided = false)
    {
        LocalEndA = localEndA;
        LocalEndB = localEndB;
        OneSided = oneSided;
    }

    public override BoundingCircle LocalBoundary
    {
        get
        {
            var mid = (LocalEndA + LocalEndB) * 0.5f;
            var half = (LocalEndA - LocalEndB).Length() * 0.5f;
            return new BoundingCircle(mid, half);
        }
    }

    public override int PrimitiveCount => 1;

    public override bool TestHit(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester)
    {
        var (a, b) = PoseEndpoints(in mine);
        if (OneSided && !IsOnOutwardSide(other.BoundingCircle.Center, a, b))
            return false;
        var p = HitPrimitive2D.Capsule(a, b, 0f);
        return other.Shape.TestHit(in other.Pose, in p, tester);
    }

    public override bool TestHit(in Pose2D mine, in HitPrimitive2D otherPrim, HitTester2D tester)
    {
        var (a, b) = PoseEndpoints(in mine);
        if (OneSided && !IsOnOutwardSide(PrimitiveCenter(in otherPrim), a, b))
            return false;
        var p = HitPrimitive2D.Capsule(a, b, 0f);
        return tester.TestHit(in p, in otherPrim);
    }

    public override bool TryGetContact(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester, out HitContact2D contact)
    {
        var (a, b) = PoseEndpoints(in mine);
        if (OneSided && !IsOnOutwardSide(other.BoundingCircle.Center, a, b))
        {
            contact = default;
            return false;
        }
        var p = HitPrimitive2D.Capsule(a, b, 0f);
        if (other.Shape.TryGetContact(in other.Pose, in p, tester, out contact))
        {
            contact = contact.Flipped();
            return true;
        }
        return false;
    }

    public override bool TryGetContact(in Pose2D mine, in HitPrimitive2D otherPrim, HitTester2D tester, out HitContact2D contact)
    {
        var (a, b) = PoseEndpoints(in mine);
        if (OneSided && !IsOnOutwardSide(PrimitiveCenter(in otherPrim), a, b))
        {
            contact = default;
            return false;
        }
        var p = HitPrimitive2D.Capsule(a, b, 0f);
        return tester.TryGetContact(in p, in otherPrim, out contact);
    }

    public override void Visit(in Pose2D mine, HitPrimitiveAction2D action)
    {
        var (a, b) = PoseEndpoints(in mine);
        var p = HitPrimitive2D.Capsule(a, b, 0f);
        action(in p);
    }

    public override HitShape2D Translate(Vector2 offset) =>
        new SegmentHitShape2D(LocalEndA + offset, LocalEndB + offset, OneSided);

    protected override HitShape2D CreateFlipped(FlipMode flip) =>
        new SegmentHitShape2D(Mirror(LocalEndA, flip), Mirror(LocalEndB, flip), OneSided);

    private (Vector2 a, Vector2 b) PoseEndpoints(in Pose2D pose) =>
        (pose.Transform(LocalEndA), pose.Transform(LocalEndB));

    // Outward direction = perp(B - A) in screen-space Y-down, matching
    // LineBarrier2D's default normal: walking A → B, outward is "left".
    private static bool IsOnOutwardSide(Vector2 point, Vector2 a, Vector2 b)
    {
        var d = b - a;
        var outward = new Vector2(d.Y, -d.X);
        return Vector2.Dot(point - a, outward) >= 0f;
    }

    private static Vector2 PrimitiveCenter(in HitPrimitive2D p) => p.Kind switch
    {
        HitKind2D.Capsule => (p.P0 + p.P1) * 0.5f,
        _ => p.P0,
    };
}
