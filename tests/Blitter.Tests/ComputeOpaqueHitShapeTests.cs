using System.Numerics;
using Blitter.Bits;

namespace Blitter.Tests;

public class ComputeOpaqueHitShapeTests
{
    private static Bitmap MakeImage(int w, int h, Action<Bitmap> draw)
    {
        var img = Bitmap.Create(w, h);
        draw(img);
        return img;
    }

    private static void FillRect(Bitmap i, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                i.SetPixel(x, y, Color.White);
    }

    [Fact]
    public void AllTransparent_ReturnsNone()
    {
        using var img = MakeImage(16, 16, _ => { });
        var shape = img.ComputeOpaqueHitShape();
        Assert.Same(HitShape.None, shape);
    }

    [Fact]
    public void SquareBlock_PrefersCircle()
    {
        using var img = MakeImage(32, 32, i => FillRect(i, 8, 8, 23, 23));
        var shape = img.ComputeOpaqueHitShape();
        Assert.IsType<CircleHitShape>(shape);
    }

    [Fact]
    public void TallNarrowRect_PrefersVerticalCapsule()
    {
        using var img = MakeImage(32, 64, i => FillRect(i, 14, 4, 17, 59));
        var shape = img.ComputeOpaqueHitShape();
        var capsule = Assert.IsType<CapsuleHitShape>(shape);
        // Endpoints should lie along the vertical axis (same X).
        Assert.Equal(capsule.LocalEndA.X, capsule.LocalEndB.X, precision: 3);
        Assert.NotEqual(capsule.LocalEndA.Y, capsule.LocalEndB.Y);
    }

    [Fact]
    public void WideShortRect_PrefersHorizontalCapsule()
    {
        using var img = MakeImage(64, 32, i => FillRect(i, 4, 14, 59, 17));
        var shape = img.ComputeOpaqueHitShape();
        var capsule = Assert.IsType<CapsuleHitShape>(shape);
        // Endpoints should lie along the horizontal axis (same Y).
        Assert.Equal(capsule.LocalEndA.Y, capsule.LocalEndB.Y, precision: 3);
        Assert.NotEqual(capsule.LocalEndA.X, capsule.LocalEndB.X);
    }

    [Fact]
    public void Circle_CoversEveryOpaquePixel()
    {
        using var img = MakeImage(40, 40, i =>
        {
            // Rough disk so the fitter picks a circle.
            for (int y = 0; y < 40; y++)
                for (int x = 0; x < 40; x++)
                {
                    var dx = x - 19.5f;
                    var dy = y - 19.5f;
                    if (dx * dx + dy * dy <= 15 * 15) i.SetPixel(x, y, Color.White);
                }
        });
        var shape = img.ComputeOpaqueHitShape();
        var circle = Assert.IsType<CircleHitShape>(shape);
        var rSq = circle.LocalRadius * circle.LocalRadius;
        for (int y = 0; y < 40; y++)
            for (int x = 0; x < 40; x++)
            {
                if (img.GetPixel(x, y).A == 0) continue;
                var p = new Vector2(x + 0.5f, y + 0.5f);
                Assert.True(Vector2.DistanceSquared(p, circle.LocalCenter) <= rSq + 1e-3f);
            }
    }

    [Fact]
    public void Capsule_CoversEveryOpaquePixel()
    {
        using var img = MakeImage(16, 64, i => FillRect(i, 4, 2, 11, 61));
        var shape = img.ComputeOpaqueHitShape();
        var capsule = Assert.IsType<CapsuleHitShape>(shape);
        var ab = capsule.LocalEndB - capsule.LocalEndA;
        var lenSq = ab.LengthSquared();
        var rSq = capsule.LocalRadius * capsule.LocalRadius;
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 16; x++)
            {
                if (img.GetPixel(x, y).A == 0) continue;
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var t = Vector2.Dot(p - capsule.LocalEndA, ab) / lenSq;
                if (t < 0f) t = 0f;
                else if (t > 1f) t = 1f;
                var d2 = Vector2.DistanceSquared(p, capsule.LocalEndA + t * ab);
                Assert.True(d2 <= rSq + 1e-3f);
            }
    }
}
