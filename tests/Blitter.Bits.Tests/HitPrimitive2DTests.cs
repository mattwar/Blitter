using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

public class HitPrimitive2DTests
{
    private const float Eps = 1e-4f;

    // ---------------- Factories / accessors ----------------

    [Fact]
    public void Circle_RoundTripsThroughAsCircle()
    {
        var c = HitPrimitive2D.Circle(new Vector2(3f, 4f), 2f);
        Assert.Equal(HitKind2D.Circle, c.Kind);
        var (center, radius) = c.AsCircle();
        Assert.Equal(new Vector2(3f, 4f), center);
        Assert.Equal(2f, radius, Eps);
    }

    [Fact]
    public void Capsule_RoundTripsThroughAsCapsule()
    {
        var cap = HitPrimitive2D.Capsule(new Vector2(0f, 0f), new Vector2(5f, 0f), 1f);
        Assert.Equal(HitKind2D.Capsule, cap.Kind);
        var (a, b, r) = cap.AsCapsule();
        Assert.Equal(new Vector2(0f, 0f), a);
        Assert.Equal(new Vector2(5f, 0f), b);
        Assert.Equal(1f, r, Eps);
    }

    [Fact]
    public void Box_RoundTripsThroughAsBox()
    {
        var box = HitPrimitive2D.Box(new Vector2(1f, 1f), new Vector2(2f, 3f), 0.5f);
        Assert.Equal(HitKind2D.Box, box.Kind);
        var (center, half, rot) = box.AsBox();
        Assert.Equal(new Vector2(1f, 1f), center);
        Assert.Equal(new Vector2(2f, 3f), half);
        Assert.Equal(0.5f, rot, Eps);
    }

    [Fact]
    public void AsBox_OnCircle_Throws()
    {
        var c = HitPrimitive2D.Circle(Vector2.Zero, 1f);
        Assert.Throws<InvalidOperationException>(() => c.AsBox());
    }

    [Fact]
    public void BoundingCircle_ImplicitlyConvertsToCirclePrimitive()
    {
        HitPrimitive2D p = new BoundingCircle(new Vector2(2f, 0f), 1.5f);
        Assert.Equal(HitKind2D.Circle, p.Kind);
        var (center, radius) = p.AsCircle();
        Assert.Equal(new Vector2(2f, 0f), center);
        Assert.Equal(1.5f, radius, Eps);
    }

    // ---------------- Intersection ----------------

    [Fact]
    public void CircleCircle_Overlapping_Intersects()
    {
        var a = HitPrimitive2D.Circle(Vector2.Zero, 1f);
        var b = HitPrimitive2D.Circle(new Vector2(1.5f, 0f), 1f);
        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void CircleCircle_Separated_DoesNotIntersect()
    {
        var a = HitPrimitive2D.Circle(Vector2.Zero, 1f);
        var b = HitPrimitive2D.Circle(new Vector2(3f, 0f), 1f);
        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void CircleCapsule_NearSegment_Intersects()
    {
        var capsule = HitPrimitive2D.Capsule(new Vector2(-2f, 0f), new Vector2(2f, 0f), 0.5f);
        var circle = HitPrimitive2D.Circle(new Vector2(0f, 0.8f), 0.5f);
        Assert.True(circle.Intersects(capsule));
        // Symmetry: order should not change the result.
        Assert.True(capsule.Intersects(circle));
    }

    [Fact]
    public void CircleCapsule_FarFromSegment_DoesNotIntersect()
    {
        var capsule = HitPrimitive2D.Capsule(new Vector2(-2f, 0f), new Vector2(2f, 0f), 0.5f);
        var circle = HitPrimitive2D.Circle(new Vector2(0f, 5f), 0.5f);
        Assert.False(circle.Intersects(capsule));
    }

    [Fact]
    public void CircleBox_CenterInside_Intersects()
    {
        var box = HitPrimitive2D.Box(Vector2.Zero, new Vector2(2f, 2f), 0f);
        var circle = HitPrimitive2D.Circle(new Vector2(0.5f, 0.5f), 0.25f);
        Assert.True(circle.Intersects(box));
    }

    [Fact]
    public void CircleBox_Outside_DoesNotIntersect()
    {
        var box = HitPrimitive2D.Box(Vector2.Zero, new Vector2(1f, 1f), 0f);
        var circle = HitPrimitive2D.Circle(new Vector2(5f, 0f), 0.5f);
        Assert.False(circle.Intersects(box));
    }

    [Fact]
    public void CapsuleCapsule_Crossing_Intersects()
    {
        var a = HitPrimitive2D.Capsule(new Vector2(-2f, 0f), new Vector2(2f, 0f), 0.2f);
        var b = HitPrimitive2D.Capsule(new Vector2(0f, -2f), new Vector2(0f, 2f), 0.2f);
        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void BoxBox_Overlapping_Intersects()
    {
        var a = HitPrimitive2D.Box(Vector2.Zero, new Vector2(1f, 1f), 0f);
        var b = HitPrimitive2D.Box(new Vector2(1.5f, 0f), new Vector2(1f, 1f), 0f);
        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void BoxBox_Separated_DoesNotIntersect()
    {
        var a = HitPrimitive2D.Box(Vector2.Zero, new Vector2(1f, 1f), 0f);
        var b = HitPrimitive2D.Box(new Vector2(5f, 0f), new Vector2(1f, 1f), 0f);
        Assert.False(a.Intersects(b));
    }

    // ---------------- Contacts ----------------

    [Fact]
    public void CircleCircleContact_NormalPointsFromSecondToFirst()
    {
        var a = HitPrimitive2D.Circle(new Vector2(1f, 0f), 1f);
        var b = HitPrimitive2D.Circle(new Vector2(-1f, 0f), 1f);
        Assert.True(a.TryGetContact(b, out var contact));
        // a is to the +X side of b, so the normal (b->a) points +X.
        Assert.Equal(1f, contact.Normal.X, 3);
        Assert.Equal(0f, contact.Normal.Y, 3);
        // Overlap depth: radii sum (2) minus center distance (2) = 0 grazing,
        // here distance is 2 so penetration ~0.
        Assert.True(contact.Penetration >= 0f);
    }

    [Fact]
    public void CircleCircleContact_PenetrationMatchesOverlap()
    {
        var a = HitPrimitive2D.Circle(new Vector2(1f, 0f), 1f);
        var b = HitPrimitive2D.Circle(new Vector2(0f, 0f), 1f);
        Assert.True(a.TryGetContact(b, out var contact));
        // Distance 1, radii sum 2 => penetration 1.
        Assert.Equal(1f, contact.Penetration, 3);
    }

    [Fact]
    public void TryGetContact_NoOverlap_ReturnsFalse()
    {
        var a = HitPrimitive2D.Circle(new Vector2(10f, 0f), 1f);
        var b = HitPrimitive2D.Circle(Vector2.Zero, 1f);
        Assert.False(a.TryGetContact(b, out _));
    }

    [Fact]
    public void TryGetContact_OrderReversed_FlipsNormal()
    {
        var circle = HitPrimitive2D.Circle(new Vector2(0f, 0f), 1f);
        var capsule = HitPrimitive2D.Capsule(new Vector2(-2f, 1f), new Vector2(2f, 1f), 0.5f);

        Assert.True(circle.TryGetContact(capsule, out var ab));
        Assert.True(capsule.TryGetContact(circle, out var ba));
        // Reversed argument order flips the contact normal.
        Assert.Equal(ab.Normal.X, -ba.Normal.X, 3);
        Assert.Equal(ab.Normal.Y, -ba.Normal.Y, 3);
    }
}
