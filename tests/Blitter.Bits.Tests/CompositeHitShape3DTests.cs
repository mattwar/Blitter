using System.Collections.Immutable;
using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

public class HitShape3DNoneTests
{
    [Fact]
    public void None_HasZeroPrimitives_AndEmptyBoundary()
    {
        Assert.Equal(0, HitShape3D.None.PrimitiveCount);
        Assert.True(HitShape3D.None.LocalBoundary.IsEmpty);
    }

    [Fact]
    public void None_NeverHits()
    {
        var none = new PosedHitShape3D(HitShape3D.None, Pose3D.Identity);
        var sphere = new PosedHitShape3D(
            new SphereHitShape3D(Vector3.Zero, 1f), Pose3D.Identity);

        Assert.False(none.TestHit(sphere));
        Assert.False(none.TryGetContact(sphere, out _));
    }

    [Fact]
    public void None_VisitEmitsNothing()
    {
        int count = 0;
        HitShape3D.None.Visit(Pose3D.Identity, (in HitPrimitive3D _) => count++);
        Assert.Equal(0, count);
    }
}

public class CompositeHitShape3DTests
{
    private const float Eps = 1e-4f;

    private static CompositeHitShape3D TwoSpheres() =>
        new(
            new SphereHitShape3D(new Vector3(-2f, 0f, 0f), 1f),
            new SphereHitShape3D(new Vector3(2f, 0f, 0f), 1f));

    [Fact]
    public void PrimitiveCount_IsSumOfSubs()
    {
        Assert.Equal(2, TwoSpheres().PrimitiveCount);
    }

    [Fact]
    public void Shapes_ExposesSubShapes()
    {
        var composite = TwoSpheres();
        Assert.Equal(2, composite.Shapes.Length);
        Assert.All(composite.Shapes, s => Assert.IsType<SphereHitShape3D>(s));
    }

    [Fact]
    public void LocalBoundary_EnclosesAllSubBoundaries()
    {
        var composite = TwoSpheres();
        var bound = composite.LocalBoundary;
        Assert.False(bound.IsEmpty);
        // Both sub-spheres reach |x|=3; the union must extend at least that far.
        Assert.True(bound.Radius + bound.Center.X >= 3f - Eps);
    }

    [Fact]
    public void Visit_EmitsOnePrimitivePerSub()
    {
        int count = 0;
        TwoSpheres().Visit(Pose3D.Identity, (in HitPrimitive3D _) => count++);
        Assert.Equal(2, count);
    }

    [Fact]
    public void DefaultImmutableArray_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CompositeHitShape3D(default(ImmutableArray<HitShape3D>)));
    }

    [Fact]
    public void TestHit_OverlappingSub_ReturnsTrue()
    {
        var composite = new PosedHitShape3D(TwoSpheres(), Pose3D.Identity);
        // Probe sphere overlapping the right sub-sphere at x=2.
        var probe = new PosedHitShape3D(
            new SphereHitShape3D(Vector3.Zero, 0.5f),
            new Pose3D(new Vector3(2f, 0f, 0f)));
        Assert.True(composite.TestHit(probe));
    }

    [Fact]
    public void TestHit_BetweenSubs_ReturnsFalse()
    {
        var composite = new PosedHitShape3D(TwoSpheres(), Pose3D.Identity);
        // Probe in the gap between the two spheres (around the origin).
        var probe = new PosedHitShape3D(
            new SphereHitShape3D(Vector3.Zero, 0.5f),
            new Pose3D(new Vector3(0f, 0f, 0f)));
        Assert.False(composite.TestHit(probe));
    }

    [Fact]
    public void TryGetContact_OverlappingSub_ReportsContact()
    {
        var composite = new PosedHitShape3D(TwoSpheres(), Pose3D.Identity);
        var probe = new PosedHitShape3D(
            new SphereHitShape3D(Vector3.Zero, 0.5f),
            new Pose3D(new Vector3(2.5f, 0f, 0f)));
        Assert.True(composite.TryGetContact(probe, out var contact));
        Assert.True(contact.Penetration > 0f);
    }
}
