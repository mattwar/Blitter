using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Callback for <see cref="PosedHitShape2D.Visit"/>. 
/// Receives the live stack-allocated primitives of a posed shape; 
/// the span is valid only for the duration of the call.
/// </summary>
public delegate void HitShapeVisitor2D(ReadOnlySpan<HitPrimitive2D> primitives);

/// <summary>
/// An abstraction of a 2D collision boundary, in image-local
/// coordinates (origin at sprite center, unrotated, unscaled).
/// </summary>
public abstract class HitShape2D
{
    /// <summary>
    /// Local-space bounding circle used for the broad-phase reject.
    /// </summary>
    public abstract BoundingCircle LocalBoundary { get; }

    /// <summary>
    /// Stage 1 of double dispatch. This shape, posed by
    /// <paramref name="mine"/>, places its world-space primitives on
    /// the stack and hands them to <paramref name="hitter"/> along
    /// with the still-posed <paramref name="other"/>; 
    /// the hitter expands <paramref name="other"/> for stage 2.
    /// </summary>
    public abstract bool TestHit(in PosedHitShape2D mine, in PosedHitShape2D other, Hitter2D hitter);

    /// <summary>
    /// Stage 2 of double dispatch. The other shape's primitives are
    /// already live in <paramref name="other"/>; this shape, posed by
    /// <paramref name="mine"/>, stackallocs its own primitives and
    /// finishes the dispatch.
    /// </summary>
    public abstract bool TestHitWith(in PosedHitShape2D mine, ReadOnlySpan<HitPrimitive2D> other, Hitter2D hitter);

    /// <summary>
    /// Hands this shape's current posed primitives to
    /// <paramref name="visitor"/>. Off the collision hot path
    /// (debug rendering, gizmos, tests).
    /// </summary>
    public abstract void Visit(in PosedHitShape2D mine, HitShapeVisitor2D visitor);

    /// <summary>
    /// Returns a copy of this shape with its local geometry shifted
    /// by <paramref name="offset"/>. Useful for changing coordinate
    /// frames (e.g. top-left-origin to image-centered).
    /// </summary>
    public abstract HitShape2D Translate(Vector2 offset);

    /// <summary>
    /// Shared "no shape" sentinel — never collides.
    /// Used by sprites with no image (e.g. raw text sprites).
    /// </summary>
    public static readonly HitShape2D None = new NoneShape();

    private sealed class NoneShape : HitShape2D
    {
        public override BoundingCircle LocalBoundary => BoundingCircle.Empty;
        public override bool TestHit(in PosedHitShape2D mine, in PosedHitShape2D other, Hitter2D hitter) => false;
        public override bool TestHitWith(in PosedHitShape2D mine, ReadOnlySpan<HitPrimitive2D> other, Hitter2D hitter) => false;
        public override void Visit(in PosedHitShape2D mine, HitShapeVisitor2D visitor) { }
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
    public BoundingCircle BroadCircle
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
    /// True when this posed shape overlaps <paramref name="other"/>.
    /// Broad-phase reject first; otherwise full primitive dispatch.
    /// </summary>
    public bool Intersects(in PosedHitShape2D other)
    {
        if (!BroadCircle.Intersects(other.BroadCircle))
            return false;
        return Shape.TestHit(in this, in other, IntersectsHitter2D.Instance);
    }

    /// <summary>
    /// Stage 1 of double dispatch using a custom <paramref name="hitter"/>.
    /// Skips the broad-phase reject; call <see cref="Intersects"/> if
    /// you want the cheap reject.
    /// </summary>
    public bool TestHit(in PosedHitShape2D other, Hitter2D hitter) =>
        Shape.TestHit(in this, in other, hitter);

    /// <summary>
    /// Hands this shape's current posed primitives to
    /// <paramref name="visitor"/>. Off the collision hot path
    /// (debug rendering, gizmos, tests).
    /// </summary>
    public void Visit(HitShapeVisitor2D visitor) =>
        Shape.Visit(in this, visitor);
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

    public override bool TestHit(in PosedHitShape2D mine, in PosedHitShape2D other, Hitter2D hitter)
    {
        Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[1];
        span[0] = Pose(in mine);
        return hitter.TestHit(span, in other);
    }

    public override bool TestHitWith(in PosedHitShape2D mine, ReadOnlySpan<HitPrimitive2D> other, Hitter2D hitter)
    {
        Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[1];
        span[0] = Pose(in mine);
        return hitter.TestHit(other, span);
    }

    public override void Visit(in PosedHitShape2D mine, HitShapeVisitor2D visitor)
    {
        Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[1];
        span[0] = Pose(in mine);
        visitor(span);
    }

    private HitPrimitive2D Pose(in PosedHitShape2D pose) =>
        HitPrimitive2D.Circle(pose.Pose.Transform(LocalCenter), LocalRadius * pose.Pose.Scale);

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

    public override bool TestHit(in PosedHitShape2D mine, in PosedHitShape2D other, Hitter2D hitter)
    {
        Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[1];
        span[0] = Pose(in mine);
        return hitter.TestHit(span, in other);
    }

    public override bool TestHitWith(in PosedHitShape2D mine, ReadOnlySpan<HitPrimitive2D> other, Hitter2D hitter)
    {
        Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[1];
        span[0] = Pose(in mine);
        return hitter.TestHit(other, span);
    }

    public override void Visit(in PosedHitShape2D mine, HitShapeVisitor2D visitor)
    {
        Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[1];
        span[0] = Pose(in mine);
        visitor(span);
    }

    private HitPrimitive2D Pose(in PosedHitShape2D pose) =>
        HitPrimitive2D.Capsule(
            pose.Pose.Transform(LocalEndA),
            pose.Pose.Transform(LocalEndB),
            LocalRadius * pose.Pose.Scale);

    public override HitShape2D Translate(Vector2 offset) =>
        new CapsuleHitShape2D(LocalEndA + offset, LocalEndB + offset, LocalRadius);
}
