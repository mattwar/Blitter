using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Callback for <see cref="PosedHitShape.Visit"/>. Receives the live
/// stack-allocated primitives of a posed shape; the span is valid only
/// for the duration of the call.
/// </summary>
public delegate void HitShapeVisitor(ReadOnlySpan<HitPrimitive> primitives);

/// <summary>
/// Pose-free description of a collision boundary, in image-local
/// coordinates (origin at sprite center, unrotated, unscaled).
/// One <see cref="HitShape"/> can be shared across many sprites and
/// across animation frames. Combine with a pose to get a
/// <see cref="PosedHitShape"/> for collision queries.
/// </summary>
/// <remarks>
/// Subclasses own their primitive layout. On dispatch they
/// <c>stackalloc</c> a span sized for their own primitives, fill it
/// using the supplied pose, and hand it to the <see cref="Hitter"/> /
/// <see cref="HitShapeVisitor"/>. The size never escapes the subclass.
/// </remarks>
public abstract class HitShape
{
    /// <summary>
    /// Local-space bounding circle used for the broad-phase reject.
    /// </summary>
    public abstract BoundingCircle LocalBoundary { get; }

    /// <summary>
    /// Stage 1 of double dispatch. This shape, posed by
    /// <paramref name="mine"/>, places its world-space primitives on
    /// the stack and hands them to <paramref name="hitter"/> along
    /// with the still-posed <paramref name="other"/>; the hitter
    /// expands <paramref name="other"/> for stage 2.
    /// </summary>
    public abstract bool TestHit(in PosedHitShape mine, in PosedHitShape other, Hitter hitter);

    /// <summary>
    /// Stage 2 of double dispatch. The other shape's primitives are
    /// already live in <paramref name="other"/>; this shape, posed by
    /// <paramref name="mine"/>, stackallocs its own primitives and
    /// finishes the dispatch.
    /// </summary>
    public abstract bool TestHitWith(in PosedHitShape mine, ReadOnlySpan<HitPrimitive> other, Hitter hitter);

    /// <summary>
    /// Hands this shape's current posed primitives to
    /// <paramref name="visitor"/>. Off the collision hot path
    /// (debug rendering, gizmos, tests).
    /// </summary>
    public abstract void Visit(in PosedHitShape mine, HitShapeVisitor visitor);

    /// <summary>
    /// Shared "no shape" sentinel — never collides.
    /// Used by sprites with no image (e.g. raw text sprites).
    /// </summary>
    public static readonly HitShape None = new NoneShape();

    private sealed class NoneShape : HitShape
    {
        public override BoundingCircle LocalBoundary => BoundingCircle.Empty;
        public override bool TestHit(in PosedHitShape mine, in PosedHitShape other, Hitter hitter) => false;
        public override bool TestHitWith(in PosedHitShape mine, ReadOnlySpan<HitPrimitive> other, Hitter hitter) => false;
        public override void Visit(in PosedHitShape mine, HitShapeVisitor visitor) { }
    }
}

/// <summary>
/// A <see cref="HitShape"/> with a world-space pose (position, rotation in degrees, scale).
/// </summary>
public readonly struct PosedHitShape
{
    /// <summary>The image-local shape.</summary>
    public readonly HitShape Shape;
    /// <summary>World-space position of the shape's local origin.</summary>
    public readonly Vector2 Position;
    /// <summary>Rotation in degrees (0 = unrotated).</summary>
    public readonly float Rotation;
    /// <summary>Uniform scale applied to the local geometry.</summary>
    public readonly float Scale;

    public PosedHitShape(HitShape shape, Vector2 position, float rotation, float scale)
    {
        Shape = shape;
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }

    /// <summary>
    /// World-space broad-phase circle, computed from <see cref="HitShape.LocalBoundary"/> and the current pose.
    /// </summary>
    public BoundingCircle BroadCircle
    {
        get
        {
            var local = Shape.LocalBoundary;
            if (local.IsEmpty)
                return new BoundingCircle(Position, 0f);
            var rad = Rotation * (MathF.PI / 180f);
            var cos = MathF.Cos(rad);
            var sin = MathF.Sin(rad);
            var scaled = local.Center * Scale;
            var offset = new Vector2(
                scaled.X * cos - scaled.Y * sin,
                scaled.X * sin + scaled.Y * cos);
            return new BoundingCircle(Position + offset, local.Radius * Scale);
        }
    }

    /// <summary>
    /// True when this posed shape overlaps <paramref name="other"/>.
    /// Broad-phase reject first; otherwise full primitive dispatch.
    /// </summary>
    public bool Intersects(in PosedHitShape other)
    {
        if (!BroadCircle.Intersects(other.BroadCircle))
            return false;
        return Shape.TestHit(in this, in other, IntersectsHitter.Instance);
    }

    /// <summary>
    /// Stage 1 of double dispatch using a custom <paramref name="hitter"/>.
    /// Skips the broad-phase reject; call <see cref="Intersects"/> if
    /// you want the cheap reject.
    /// </summary>
    public bool TestHit(in PosedHitShape other, Hitter hitter) =>
        Shape.TestHit(in this, in other, hitter);

    /// <summary>
    /// Hands this shape's current posed primitives to
    /// <paramref name="visitor"/>. Off the collision hot path
    /// (debug rendering, gizmos, tests).
    /// </summary>
    public void Visit(HitShapeVisitor visitor) =>
        Shape.Visit(in this, visitor);
}

/// <summary>
/// A <see cref="HitShape"/> that is a single circle.
/// The center and radius are in image-local coordinates.
/// </summary>
public sealed class CircleHitShape : HitShape
{
    public Vector2 LocalCenter { get; }
    public float LocalRadius { get; }

    public CircleHitShape(Vector2 localCenter, float localRadius)
    {
        LocalCenter = localCenter;
        LocalRadius = localRadius;
    }

    public override BoundingCircle LocalBoundary => new(LocalCenter, LocalRadius);

    public override bool TestHit(in PosedHitShape mine, in PosedHitShape other, Hitter hitter)
    {
        Span<HitPrimitive> span = stackalloc HitPrimitive[1];
        span[0] = Pose(in mine);
        return hitter.TestHit(span, in other);
    }

    public override bool TestHitWith(in PosedHitShape mine, ReadOnlySpan<HitPrimitive> other, Hitter hitter)
    {
        Span<HitPrimitive> span = stackalloc HitPrimitive[1];
        span[0] = Pose(in mine);
        return hitter.TestHit(other, span);
    }

    public override void Visit(in PosedHitShape mine, HitShapeVisitor visitor)
    {
        Span<HitPrimitive> span = stackalloc HitPrimitive[1];
        span[0] = Pose(in mine);
        visitor(span);
    }

    private HitPrimitive Pose(in PosedHitShape pose)
    {
        var rad = pose.Rotation * (MathF.PI / 180f);
        var cos = MathF.Cos(rad);
        var sin = MathF.Sin(rad);
        var local = LocalCenter * pose.Scale;
        var offset = new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
        return HitPrimitive.Circle(pose.Position + offset, LocalRadius * pose.Scale);
    }
}

/// <summary>
/// A hit shape <see cref="HitShape"/> that is a capsule.
/// It is the Minkowski sum of segment <see cref="LocalEndA"/>–<see cref="LocalEndB"/> 
/// with a disk of <see cref="LocalRadius"/>. 
/// Endpoints are in image-local coordinates.
/// </summary>
public sealed class CapsuleHitShape : HitShape
{
    public Vector2 LocalEndA { get; }
    public Vector2 LocalEndB { get; }
    public float LocalRadius { get; }

    public CapsuleHitShape(Vector2 localEndA, Vector2 localEndB, float localRadius)
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

    public override bool TestHit(in PosedHitShape mine, in PosedHitShape other, Hitter hitter)
    {
        Span<HitPrimitive> span = stackalloc HitPrimitive[1];
        span[0] = Pose(in mine);
        return hitter.TestHit(span, in other);
    }

    public override bool TestHitWith(in PosedHitShape mine, ReadOnlySpan<HitPrimitive> other, Hitter hitter)
    {
        Span<HitPrimitive> span = stackalloc HitPrimitive[1];
        span[0] = Pose(in mine);
        return hitter.TestHit(other, span);
    }

    public override void Visit(in PosedHitShape mine, HitShapeVisitor visitor)
    {
        Span<HitPrimitive> span = stackalloc HitPrimitive[1];
        span[0] = Pose(in mine);
        visitor(span);
    }

    private HitPrimitive Pose(in PosedHitShape pose)
    {
        var rad = pose.Rotation * (MathF.PI / 180f);
        var cos = MathF.Cos(rad);
        var sin = MathF.Sin(rad);
        var a = LocalEndA * pose.Scale;
        var b = LocalEndB * pose.Scale;
        var ra = new Vector2(a.X * cos - a.Y * sin, a.X * sin + a.Y * cos);
        var rb = new Vector2(b.X * cos - b.Y * sin, b.X * sin + b.Y * cos);
        return HitPrimitive.Capsule(pose.Position + ra, pose.Position + rb, LocalRadius * pose.Scale);
    }
}
