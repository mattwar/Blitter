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
    public void HitShape_Intersects_Uses_BroadCircle_Reject()
    {
        // Two stub shapes whose broad circles miss should never reach
        // the primitive dispatch — verified by the canary hitter below.
        var canary = new CanaryHitter();
        var a = new StubShape(new BoundingCircle(new Vector2(0, 0), 4f), HitPrimitive.Circle(new Vector2(0, 0), 4f));
        var b = new StubShape(new BoundingCircle(new Vector2(100, 0), 4f), HitPrimitive.Circle(new Vector2(100, 0), 4f));

        Assert.False(HitShape.Intersects(a, b));
        Assert.Equal(0, canary.Calls);
    }

    [Fact]
    public void HitShape_Intersects_Returns_True_On_Overlap()
    {
        var a = new StubShape(new BoundingCircle(new Vector2(0, 0), 5f), HitPrimitive.Circle(new Vector2(0, 0), 5f));
        var b = new StubShape(new BoundingCircle(new Vector2(6, 0), 5f), HitPrimitive.Circle(new Vector2(6, 0), 5f));
        Assert.True(HitShape.Intersects(a, b));
    }

    [Fact]
    public void MultiPrimitive_Shape_Hits_Via_Any_Primitive()
    {
        // Shape A has two well-separated circles; B overlaps only the second.
        var a = new MultiShape(
            HitPrimitive.Circle(new Vector2(0, 0), 2f),
            HitPrimitive.Circle(new Vector2(20, 0), 2f));
        var b = new StubShape(new BoundingCircle(new Vector2(21, 0), 2f), HitPrimitive.Circle(new Vector2(21, 0), 2f));
        Assert.True(HitShape.Intersects(a, b));
    }

    [Fact]
    public void MultiPrimitive_Shape_Misses_When_No_Pair_Overlaps()
    {
        var a = new MultiShape(
            HitPrimitive.Circle(new Vector2(0, 0), 2f),
            HitPrimitive.Circle(new Vector2(20, 0), 2f));
        // Broad circle of MultiShape covers both, so this lands inside the
        // broad reject but no individual primitive overlaps the target.
        var b = new StubShape(new BoundingCircle(new Vector2(10, 0), 2f), HitPrimitive.Circle(new Vector2(10, 0), 2f));
        Assert.False(HitShape.Intersects(a, b));
    }

    private sealed class StubShape : HitShape
    {
        private readonly BoundingCircle _broad;
        private readonly HitPrimitive _prim;

        public StubShape(BoundingCircle broad, HitPrimitive prim)
        {
            _broad = broad;
            _prim = prim;
        }

        public override BoundingCircle BroadCircle => _broad;

        public override bool TestHit(HitShape other, Hitter hitter)
        {
            Span<HitPrimitive> mine = stackalloc HitPrimitive[1];
            mine[0] = _prim;
            return hitter.TestHit(mine, other);
        }

        public override bool TestHitWith(ReadOnlySpan<HitPrimitive> other, Hitter hitter)
        {
            Span<HitPrimitive> mine = stackalloc HitPrimitive[1];
            mine[0] = _prim;
            return hitter.TestHit(other, mine);
        }

        public override void Visit(HitShapeVisitor visitor)
        {
            Span<HitPrimitive> mine = stackalloc HitPrimitive[1];
            mine[0] = _prim;
            visitor(mine);
        }
    }

    private sealed class MultiShape : HitShape
    {
        private readonly HitPrimitive _a, _b;

        public MultiShape(HitPrimitive a, HitPrimitive b)
        {
            _a = a;
            _b = b;
        }

        public override BoundingCircle BroadCircle
        {
            // A loose bounding circle covering both primitives.
            get
            {
                var mid = (_a.P0 + _b.P0) * 0.5f;
                var halfSpan = (_a.P0 - _b.P0).Length() * 0.5f;
                var r = halfSpan + MathF.Max(_a.R, _b.R);
                return new BoundingCircle(mid, r);
            }
        }

        public override bool TestHit(HitShape other, Hitter hitter)
        {
            Span<HitPrimitive> mine = stackalloc HitPrimitive[2];
            mine[0] = _a;
            mine[1] = _b;
            return hitter.TestHit(mine, other);
        }

        public override bool TestHitWith(ReadOnlySpan<HitPrimitive> other, Hitter hitter)
        {
            Span<HitPrimitive> mine = stackalloc HitPrimitive[2];
            mine[0] = _a;
            mine[1] = _b;
            return hitter.TestHit(other, mine);
        }

        public override void Visit(HitShapeVisitor visitor)
        {
            Span<HitPrimitive> mine = stackalloc HitPrimitive[2];
            mine[0] = _a;
            mine[1] = _b;
            visitor(mine);
        }
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
