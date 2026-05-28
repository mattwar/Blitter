using System.Numerics;
using Blitter.Bits;

namespace Blitter.Tests;

public class HitPrimitive3DTriangleTests
{
    private const float Eps = 1e-4f;

    // A flat triangle in the XZ plane (y=0); winding chosen so the
    // face normal (right-hand rule) is +Y.
    private static (Vector3 v0, Vector3 v1, Vector3 v2) FlatTriangle() =>
        (new Vector3(-1, 0, -1), new Vector3(0, 0, 1), new Vector3(1, 0, -1));

    private static HitPrimitive3D TriangleAbove() =>
        HitPrimitive3D.Triangle(
            new Vector3(-1, 0, -1),
            new Vector3(0, 0, 1),
            new Vector3(1, 0, -1));

    // ---- Capsule × Triangle ----

    [Fact]
    public void Capsule_AboveTriangle_Far_NoHit()
    {
        var cap = HitPrimitive3D.Capsule(new Vector3(0, 5, 0), new Vector3(0, 6, 0), 0.5f);
        var tri = TriangleAbove();
        Assert.False(cap.Intersects(in tri));
        Assert.False(cap.TryGetContact(in tri, out _));
    }

    [Fact]
    public void Capsule_AboveTriangle_JustTouching_HitAndContactNormalUp()
    {
        // Capsule bottom hemisphere sits at y=0.3 with radius=0.5 → bottom reaches y=-0.2 (overlaps plane).
        var cap = HitPrimitive3D.Capsule(new Vector3(0, 0.3f, 0), new Vector3(0, 1.3f, 0), 0.5f);
        var tri = TriangleAbove();
        Assert.True(cap.Intersects(in tri));
        Assert.True(cap.TryGetContact(in tri, out var c));
        // Normal points from triangle (below) toward capsule (above) → +Y.
        Assert.True(c.Normal.Y > 0.99f, $"Normal.Y was {c.Normal.Y}");
        Assert.InRange(c.Penetration, 0.19f, 0.21f);
    }

    [Fact]
    public void Capsule_PiercingTriangle_ContactUsesFaceNormal()
    {
        // Vertical capsule centered through the triangle plane.
        var cap = HitPrimitive3D.Capsule(new Vector3(0, -0.5f, 0), new Vector3(0, 0.5f, 0), 0.3f);
        var tri = TriangleAbove();
        Assert.True(cap.TryGetContact(in tri, out var c));
        // Fallback uses face normal (+Y).
        Assert.True(c.Normal.Y > 0.99f);
    }

    [Fact]
    public void Capsule_LowerEndpointBelowTrianglePlane_NormalStaysUp()
    {
        // Regression: when the capsule's bottom segment endpoint dips
        // slightly below the triangle's plane (e.g. one frame of
        // gravity during walking), the contact normal must still point
        // +Y, not -Y. Penetration must report R + |signed dist|, not
        // R - |signed dist|.
        //   bottom endpoint at y=-0.05, radius 0.3 → bottom of sphere
        //   reaches y=-0.35.
        var cap = HitPrimitive3D.Capsule(new Vector3(0, -0.05f, 0), new Vector3(0, 0.95f, 0), 0.3f);
        var tri = TriangleAbove();
        Assert.True(cap.TryGetContact(in tri, out var c));
        Assert.True(c.Normal.Y > 0.99f, $"Normal.Y was {c.Normal.Y}");
        Assert.InRange(c.Penetration, 0.34f, 0.36f);
    }

    [Fact]
    public void Sphere_CenterBelowTrianglePlane_NormalStaysUp()
    {
        // Same regression for the sphere face-contact path.
        var sph = HitPrimitive3D.Sphere(new Vector3(0, -0.05f, 0), 0.3f);
        var tri = TriangleAbove();
        Assert.True(sph.TryGetContact(in tri, out var c));
        Assert.True(c.Normal.Y > 0.99f, $"Normal.Y was {c.Normal.Y}");
        Assert.InRange(c.Penetration, 0.34f, 0.36f);
    }

    [Fact]
    public void Triangle_CapsuleReversed_FlipsNormal()
    {
        var cap = HitPrimitive3D.Capsule(new Vector3(0, 0.3f, 0), new Vector3(0, 1.3f, 0), 0.5f);
        var tri = TriangleAbove();
        Assert.True(cap.TryGetContact(in tri, out var forward));
        Assert.True(tri.TryGetContact(in cap, out var reverse));
        Assert.True(Vector3.Dot(forward.Normal, reverse.Normal) < -0.99f);
    }

    // ---- Cylinder × Triangle ----

    [Fact]
    public void Cylinder_OverlappingTriangle_Hit()
    {
        var cyl = HitPrimitive3D.Cylinder(new Vector3(0, -0.2f, 0), new Vector3(0, 0.2f, 0), 0.5f);
        var tri = TriangleAbove();
        Assert.True(cyl.Intersects(in tri));
        Assert.True(cyl.TryGetContact(in tri, out _));
    }

    // ---- Box × Triangle ----

    [Fact]
    public void Box_AboveTriangle_NoHit()
    {
        var box = HitPrimitive3D.Box(new Vector3(0, 5, 0), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.Identity);
        var tri = TriangleAbove();
        Assert.False(box.Intersects(in tri));
        Assert.False(box.TryGetContact(in tri, out _));
    }

    [Fact]
    public void Box_RestingOnTriangle_HitWithUpNormal()
    {
        // Box bottom at y=-0.1 overlaps the triangle plane by 0.1.
        var box = HitPrimitive3D.Box(new Vector3(0, 0.4f, 0), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.Identity);
        var tri = TriangleAbove();
        Assert.True(box.Intersects(in tri));
        Assert.True(box.TryGetContact(in tri, out var c));
        Assert.True(c.Normal.Y > 0.99f, $"Normal.Y was {c.Normal.Y}");
        Assert.InRange(c.Penetration, 0.09f, 0.11f);
    }

    [Fact]
    public void Box_StraddlingTriangleEdge_Hit()
    {
        // Triangle edge from (-1,0,-1) to (1,0,-1) along x. Place box on that edge.
        var box = HitPrimitive3D.Box(new Vector3(0, 0.4f, -1f), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.Identity);
        var tri = TriangleAbove();
        Assert.True(box.Intersects(in tri));
        Assert.True(box.TryGetContact(in tri, out _));
    }

    [Fact]
    public void Box_FarFromTriangle_NoHit()
    {
        var box = HitPrimitive3D.Box(new Vector3(10, 10, 10), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.Identity);
        var tri = TriangleAbove();
        Assert.False(box.Intersects(in tri));
    }

    // ---- Wall × Triangle ----

    [Fact]
    public void Wall_OverlappingTriangle_Hit()
    {
        // Wall in XY plane at origin (normal +Z), 2x2.
        var wall = HitPrimitive3D.Wall(Vector3.Zero, new Vector2(1f, 1f), Quaternion.Identity);
        // Triangle that straddles z=0.
        var tri = HitPrimitive3D.Triangle(
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0f, 0.5f, 0f));
        Assert.True(wall.Intersects(in tri));
        Assert.True(wall.TryGetContact(in tri, out _));
    }

    [Fact]
    public void Wall_TriangleEntirelyOnOneSide_NoHit()
    {
        var wall = HitPrimitive3D.Wall(Vector3.Zero, new Vector2(1f, 1f), Quaternion.Identity);
        var tri = HitPrimitive3D.Triangle(
            new Vector3(-0.5f, -0.5f, 1f),
            new Vector3(0.5f, -0.5f, 1f),
            new Vector3(0f, 0.5f, 1f));
        Assert.False(wall.Intersects(in tri));
    }
}
