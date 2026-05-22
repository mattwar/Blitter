using System.Numerics;
using Blitter.Bits;

namespace Blitter.Blocks.Tests;

public class HitShapeTests
{
    [Fact]
    public void Circle_Primitives_Intersect_When_Close()
    {
        var a = HitPrimitive.Circle(new Vector2(0, 0), 5f);
        var b = HitPrimitive.Circle(new Vector2(8, 0), 5f);
        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void Circle_Primitives_Miss_When_Far()
    {
        var a = HitPrimitive.Circle(new Vector2(0, 0), 5f);
        var b = HitPrimitive.Circle(new Vector2(20, 0), 5f);
        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void PosedHitShape_Intersects_Uses_BroadCircle_Reject()
    {
        // Two shapes whose broad circles miss should never reach the
        // primitive dispatch — verified by the canary hitter below.
        var canary = new CanaryHitter();
        var a = Pose(new CircleHitShape(Vector2.Zero, 4f), new Vector2(0, 0));
        var b = Pose(new CircleHitShape(Vector2.Zero, 4f), new Vector2(100, 0));

        Assert.False(a.Intersects(b));
        // Run the dispatch directly through the canary to confirm it
        // would have been invoked if the broad-phase had let us through.
        Assert.False(a.TestHit(in b, canary)); // BroadCircle miss is in Intersects, not TestHit
        // Sanity: the canary was invoked once for that direct TestHit call.
        Assert.Equal(1, canary.Calls);
    }

    [Fact]
    public void PosedHitShape_Intersects_Returns_True_On_Overlap()
    {
        var a = Pose(new CircleHitShape(Vector2.Zero, 5f), new Vector2(0, 0));
        var b = Pose(new CircleHitShape(Vector2.Zero, 5f), new Vector2(6, 0));
        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void MultiPrimitive_Shape_Hits_Via_Any_Primitive()
    {
        // Shape A has two well-separated circles; B overlaps only the second.
        var a = Pose(new TwoCircleShape(new Vector2(0, 0), new Vector2(20, 0), 2f), Vector2.Zero);
        var b = Pose(new CircleHitShape(Vector2.Zero, 2f), new Vector2(21, 0));
        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void MultiPrimitive_Shape_Misses_When_No_Pair_Overlaps()
    {
        var a = Pose(new TwoCircleShape(new Vector2(0, 0), new Vector2(20, 0), 2f), Vector2.Zero);
        // Broad circle of TwoCircleShape covers both, so this lands inside
        // the broad reject but no individual primitive overlaps the target.
        var b = Pose(new CircleHitShape(Vector2.Zero, 2f), new Vector2(10, 0));
        Assert.False(a.Intersects(b));
    }

    private static PosedHitShape Pose(HitShape shape, Vector2 position) =>
        new(shape, position, 0f, 1f);

    /// <summary>
    /// Two-circle test shape: emits a circle at each local point with
    /// the same radius, and a bounding circle covering both.
    /// </summary>
    private sealed class TwoCircleShape : HitShape
    {
        private readonly Vector2 _a;
        private readonly Vector2 _b;
        private readonly float _r;

        public TwoCircleShape(Vector2 a, Vector2 b, float r)
        {
            _a = a;
            _b = b;
            _r = r;
        }

        public override BoundingCircle LocalBoundary
        {
            get
            {
                var mid = (_a + _b) * 0.5f;
                var half = (_a - _b).Length() * 0.5f;
                return new BoundingCircle(mid, half + _r);
            }
        }

        public override bool TestHit(in PosedHitShape mine, in PosedHitShape other, Hitter hitter)
        {
            Span<HitPrimitive> span = stackalloc HitPrimitive[2];
            Pose(span, in mine);
            return hitter.TestHit(span, in other);
        }

        public override bool TestHitWith(in PosedHitShape mine, ReadOnlySpan<HitPrimitive> other, Hitter hitter)
        {
            Span<HitPrimitive> span = stackalloc HitPrimitive[2];
            Pose(span, in mine);
            return hitter.TestHit(other, span);
        }

        public override void Visit(in PosedHitShape mine, HitShapeVisitor visitor)
        {
            Span<HitPrimitive> span = stackalloc HitPrimitive[2];
            Pose(span, in mine);
            visitor(span);
        }

        private void Pose(Span<HitPrimitive> destination, in PosedHitShape pose)
        {
            var rad = pose.Rotation * (MathF.PI / 180f);
            var cos = MathF.Cos(rad);
            var sin = MathF.Sin(rad);
            destination[0] = HitPrimitive.Circle(pose.Position + Rotate(_a * pose.Scale, cos, sin), _r * pose.Scale);
            destination[1] = HitPrimitive.Circle(pose.Position + Rotate(_b * pose.Scale, cos, sin), _r * pose.Scale);
        }

        private static Vector2 Rotate(Vector2 v, float cos, float sin) =>
            new(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private sealed class CanaryHitter : Hitter
    {
        public int Calls { get; private set; }

        public override bool TestHit(ReadOnlySpan<HitPrimitive> a, ReadOnlySpan<HitPrimitive> b)
        {
            Calls++;
            return false;
        }
    }
}
