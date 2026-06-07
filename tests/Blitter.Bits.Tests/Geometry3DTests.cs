using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

public class Geometry3DTests
{
    private const float Eps = 1e-4f;

    private static readonly Vector3 A = new(0f, 0f, 0f);
    private static readonly Vector3 B = new(1f, 0f, 0f);
    private static readonly Vector3 C = new(0f, 1f, 0f);

    [Fact]
    public void ClosestPointOnTriangle_PointAboveInterior_ProjectsOntoFace()
    {
        // Point hovering over the interior projects straight down to the plane.
        var p = new Vector3(0.25f, 0.25f, 5f);
        var got = Geometry3D.ClosestPointOnTriangle(p, A, B, C);
        Assert.Equal(0.25f, got.X, Eps);
        Assert.Equal(0.25f, got.Y, Eps);
        Assert.Equal(0f, got.Z, Eps);
    }

    [Fact]
    public void ClosestPointOnTriangle_BeyondVertex_ReturnsVertex()
    {
        var p = new Vector3(-3f, -3f, 0f);
        var got = Geometry3D.ClosestPointOnTriangle(p, A, B, C);
        Assert.Equal(A, got);
    }

    [Fact]
    public void ClosestPointOnTriangle_BesideEdge_ReturnsPointOnEdge()
    {
        // Point to the right of edge AB clamps onto the edge.
        var p = new Vector3(0.5f, -2f, 0f);
        var got = Geometry3D.ClosestPointOnTriangle(p, A, B, C);
        Assert.Equal(0.5f, got.X, Eps);
        Assert.Equal(0f, got.Y, Eps);
        Assert.Equal(0f, got.Z, Eps);
    }

    [Fact]
    public void PointSegmentDistanceSquared_PointOnSegment_IsZero()
    {
        var d = Geometry3D.PointSegmentDistanceSquared(
            new Vector3(0.5f, 0f, 0f), A, B);
        Assert.Equal(0f, d, Eps);
    }

    [Fact]
    public void PointSegmentDistanceSquared_Perpendicular_IsDistanceSquared()
    {
        // 2 units above the midpoint => squared distance 4.
        var d = Geometry3D.PointSegmentDistanceSquared(
            new Vector3(0.5f, 2f, 0f), A, B);
        Assert.Equal(4f, d, Eps);
    }

    [Fact]
    public void PointSegmentDistanceSquared_BeyondEndpoint_ClampsToEndpoint()
    {
        // Past B along X => distance to B, squared.
        var d = Geometry3D.PointSegmentDistanceSquared(
            new Vector3(4f, 0f, 0f), A, B);
        Assert.Equal(9f, d, Eps);
    }

    [Fact]
    public void PointSegmentDistanceSquared_DegenerateSegment_UsesEndpoint()
    {
        var d = Geometry3D.PointSegmentDistanceSquared(
            new Vector3(3f, 0f, 0f), A, A);
        Assert.Equal(9f, d, Eps);
    }

    [Fact]
    public void SegmentSegmentDistanceSquared_CrossingSegments_IsZero()
    {
        // Two segments that cross at the origin in the z=0 plane.
        var d = Geometry3D.SegmentSegmentDistanceSquared(
            new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f),
            new Vector3(0f, -1f, 0f), new Vector3(0f, 1f, 0f));
        Assert.Equal(0f, d, Eps);
    }

    [Fact]
    public void SegmentSegmentDistanceSquared_ParallelOffset_IsGapSquared()
    {
        // Parallel segments one unit apart along Y.
        var d = Geometry3D.SegmentSegmentDistanceSquared(
            new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f));
        Assert.Equal(1f, d, Eps);
    }

    [Fact]
    public void SegmentSegmentClosestPoints_ReturnsPointsOnEachSegment()
    {
        var d = Geometry3D.SegmentSegmentClosestPoints(
            new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
            new Vector3(0f, 2f, 0f), new Vector3(1f, 2f, 0f),
            out var c1, out var c2);
        Assert.Equal(4f, d, Eps);
        Assert.Equal(0f, c1.Y, Eps);
        Assert.Equal(2f, c2.Y, Eps);
        // The two closest points share the same X on these parallel lines.
        Assert.Equal(c1.X, c2.X, Eps);
    }

    [Fact]
    public void PointInTriangle_InteriorPoint_IsTrue()
    {
        Assert.True(Geometry3D.PointInTriangle(
            new Vector3(0.25f, 0.25f, 0f), A, B, C));
    }

    [Fact]
    public void PointInTriangle_OutsidePoint_IsFalse()
    {
        Assert.False(Geometry3D.PointInTriangle(
            new Vector3(1f, 1f, 0f), A, B, C));
    }

    [Fact]
    public void PointInTriangle_DegenerateTriangle_IsFalse()
    {
        Assert.False(Geometry3D.PointInTriangle(
            new Vector3(0.25f, 0.25f, 0f), A, A, A));
    }

    [Fact]
    public void SegmentTriangleClosestPoints_PiercingSegment_IsZero()
    {
        // Segment passes through the triangle interior along Z.
        var d = Geometry3D.SegmentTriangleClosestPoints(
            new Vector3(0.25f, 0.25f, -1f), new Vector3(0.25f, 0.25f, 1f),
            A, B, C, out var cs, out var ct);
        Assert.Equal(0f, d, Eps);
        Assert.Equal(cs, ct);
    }

    [Fact]
    public void SegmentTriangleClosestPoints_SeparatedSegment_IsGapSquared()
    {
        // Segment parallel to the triangle plane, 3 units above it.
        var d = Geometry3D.SegmentTriangleClosestPoints(
            new Vector3(0.25f, 0.25f, 3f), new Vector3(0.3f, 0.3f, 3f),
            A, B, C, out _, out _);
        Assert.Equal(9f, d, Eps);
    }
}
