using System.Numerics;
using Blitter.Bits;

namespace Blitter.Blocks.Tests;

public class HitShapeTests
{
    [Fact]
    public void Circle_Primitives_Intersect_When_Close()
    {
        var a = HitPrimitive2D.Circle(new Vector2(0, 0), 5f);
        var b = HitPrimitive2D.Circle(new Vector2(8, 0), 5f);
        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void Circle_Primitives_Miss_When_Far()
    {
        var a = HitPrimitive2D.Circle(new Vector2(0, 0), 5f);
        var b = HitPrimitive2D.Circle(new Vector2(20, 0), 5f);
        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void PosedHitShape_TestHit_Uses_BoundingCircle_Reject()
    {
        // Two shapes whose bounding circles miss should never reach the
        // primitive dispatch — verified by the canary tester below.
        var canary = new CanaryHitTester();
        var a = Pose(new CircleHitShape2D(Vector2.Zero, 4f), new Vector2(0, 0));
        var b = Pose(new CircleHitShape2D(Vector2.Zero, 4f), new Vector2(100, 0));

        Assert.False(a.TestHit(in b, canary));
        Assert.Equal(0, canary.Calls); // broad-phase reject short-circuited dispatch

        // Driving the tester directly bypasses the reject and reaches dispatch.
        Assert.False(canary.TestHit(in a, in b));
        Assert.Equal(1, canary.Calls);
    }

    [Fact]
    public void PosedHitShape_TestHit_Returns_True_On_Overlap()
    {
        var a = Pose(new CircleHitShape2D(Vector2.Zero, 5f), new Vector2(0, 0));
        var b = Pose(new CircleHitShape2D(Vector2.Zero, 5f), new Vector2(6, 0));
        Assert.True(a.TestHit(b));
    }

    [Fact]
    public void MultiPrimitive_Shape_Hits_Via_Any_Primitive()
    {
        // Shape A has two well-separated circles; B overlaps only the second.
        var a = Pose(new TwoCircleShape(new Vector2(0, 0), new Vector2(20, 0), 2f), Vector2.Zero);
        var b = Pose(new CircleHitShape2D(Vector2.Zero, 2f), new Vector2(21, 0));
        Assert.True(a.TestHit(b));
    }

    [Fact]
    public void MultiPrimitive_Shape_Misses_When_No_Pair_Overlaps()
    {
        var a = Pose(new TwoCircleShape(new Vector2(0, 0), new Vector2(20, 0), 2f), Vector2.Zero);
        // Bounding circle of TwoCircleShape covers both, so this lands inside
        // the broad reject but no individual primitive overlaps the target.
        var b = Pose(new CircleHitShape2D(Vector2.Zero, 2f), new Vector2(10, 0));
        Assert.False(a.TestHit(b));
    }

    [Fact]
    public void HorizontalFlip_Mirrors_Asymmetric_Circle()
    {
        // Asymmetric circle: local center at (5, 0). Un-flipped at world
        // origin sits at (5, 0). The horizontally-flipped sibling lives
        // at (-5, 0) in local space and so projects to (-5, 0) at origin.
        var shape = new CircleHitShape2D(new Vector2(5, 0), 1f);
        var flipped = new PosedHitShape2D(shape.Flipped(FlipMode.Horizontal), new Pose2D(Vector2.Zero));
        Assert.Equal(-5f, flipped.BoundingCircle.Center.X, precision: 3);
        Assert.Equal(0f, flipped.BoundingCircle.Center.Y, precision: 3);
    }

    [Fact]
    public void VerticalFlip_Mirrors_Asymmetric_Circle()
    {
        var shape = new CircleHitShape2D(new Vector2(0, 5), 1f);
        var flipped = new PosedHitShape2D(shape.Flipped(FlipMode.Vertical), new Pose2D(Vector2.Zero));
        Assert.Equal(0f, flipped.BoundingCircle.Center.X, precision: 3);
        Assert.Equal(-5f, flipped.BoundingCircle.Center.Y, precision: 3);
    }

    [Fact]
    public void HorizontalFlip_Mirrors_Capsule_Endpoints()
    {
        // Capsule from (5,0) to (15,0). Flipping horizontally and placing
        // at world origin should turn it into (-5,0)→(-15,0). A probe at
        // +10 hits unflipped, misses flipped; at -10 the opposite.
        var capsule = new CapsuleHitShape2D(new Vector2(5, 0), new Vector2(15, 0), 1f);
        var unflipped = new PosedHitShape2D(capsule, new Pose2D(Vector2.Zero));
        var flipped = new PosedHitShape2D(capsule.Flipped(FlipMode.Horizontal), new Pose2D(Vector2.Zero));
        var probePos = Pose(new CircleHitShape2D(Vector2.Zero, 1f), new Vector2(10, 0));
        var probeNeg = Pose(new CircleHitShape2D(Vector2.Zero, 1f), new Vector2(-10, 0));

        Assert.True(unflipped.TestHit(probePos));
        Assert.False(unflipped.TestHit(probeNeg));
        Assert.False(flipped.TestHit(probePos));
        Assert.True(flipped.TestHit(probeNeg));
    }

    [Fact]
    public void Flipping_A_Flipped_Shape_Returns_Through_Canonical()
    {
        // Klein-4 group: flipping H by H should land back on the canonical
        // instance; flipping H by V should land on the H+V sibling. The
        // family is interned, so repeated traversals return the same refs.
        var canonical = new CapsuleHitShape2D(new Vector2(5, 0), new Vector2(15, 0), 1f);
        var h = canonical.Flipped(FlipMode.Horizontal);
        var v = canonical.Flipped(FlipMode.Vertical);
        var hv = canonical.Flipped(FlipMode.Both);

        Assert.Same(canonical, h.Flipped(FlipMode.Horizontal));
        Assert.Same(canonical, v.Flipped(FlipMode.Vertical));
        Assert.Same(canonical, hv.Flipped(FlipMode.Both));
        Assert.Same(hv, h.Flipped(FlipMode.Vertical));
        Assert.Same(v,  h.Flipped(FlipMode.Both));
        Assert.Same(h,  v.Flipped(FlipMode.Both));
    }

    [Fact]
    public void Flipped_None_Returns_Self()
    {
        var shape = new CircleHitShape2D(new Vector2(5, 0), 1f);
        Assert.Same(shape, shape.Flipped(FlipMode.None));
    }

    private static PosedHitShape2D Pose(HitShape2D shape, Vector2 position) =>
        new(shape, new Pose2D(position));

    /// <summary>
    /// Two-circle test shape: emits a circle at each local point with
    /// the same radius, and a bounding circle covering both.
    /// </summary>
    private sealed class TwoCircleShape : HitShape2D
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

        public override bool TestHit(in Pose2D mine, in PosedHitShape2D other, HitTester2D tester)
        {
            Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[2];
            Pose(span, in mine);
            return tester.TestHit(span, in other);
        }

        public override bool TestHitWith(in Pose2D mine, ReadOnlySpan<HitPrimitive2D> other, HitTester2D tester)
        {
            Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[2];
            Pose(span, in mine);
            return tester.TestHit(other, span);
        }

        public override void Visit(in Pose2D mine, HitShapeVisitor2D visitor)
        {
            Span<HitPrimitive2D> span = stackalloc HitPrimitive2D[2];
            Pose(span, in mine);
            visitor(span);
        }

        private void Pose(Span<HitPrimitive2D> destination, in Pose2D pose)
        {
            destination[0] = HitPrimitive2D.Circle(pose.Transform(_a), _r * pose.Scale);
            destination[1] = HitPrimitive2D.Circle(pose.Transform(_b), _r * pose.Scale);
        }

        public override HitShape2D Translate(Vector2 offset) =>
            new TwoCircleShape(_a + offset, _b + offset, _r);

        protected override HitShape2D CreateFlipped(FlipMode flip) =>
            new TwoCircleShape(Mirror(_a, flip), Mirror(_b, flip), _r);
    }

    private sealed class CanaryHitTester : HitTester2D
    {
        public int Calls { get; private set; }

        public override bool TestHit(ReadOnlySpan<HitPrimitive2D> a, ReadOnlySpan<HitPrimitive2D> b)
        {
            Calls++;
            return false;
        }
    }
}
