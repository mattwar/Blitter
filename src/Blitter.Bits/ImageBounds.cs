using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Helpers to compute bounds for an <see cref="Texture2D"/>.
/// </summary>
public static class ImageBounds
{
    /// <summary>
    /// Gets the minimum axis-aligned bounding rectangle that contains every pixel.
    /// </summary>
    public static BoundingRect ComputeOpaqueBounds(this Bitmap image, byte alphaThreshold = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        if (w <= 0 || h <= 0) return BoundingRect.Empty;

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        // Single pass tracking min/max of opaque pixel coords.
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (image.GetPixel(x, y).A > alphaThreshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (minX == int.MaxValue) return BoundingRect.Empty;

        // Half-open [Min, Max): a single opaque pixel at (5,5) yields
        // Min=(5,5), Max=(6,6) -- size 1x1.
        return new BoundingRect(
            new Vector2(minX, minY),
            new Vector2(maxX + 1, maxY + 1));
    }

    /// <summary>
    /// Gets a tight bounding circle for the opaque pixels using
    /// Ritter's algorithm. Handles off-center / asymmetric shapes
    /// far better than the rect-circumscribing circle.
    /// </summary>
    public static BoundingCircle ComputeOpaqueCircle(this Bitmap image, byte alphaThreshold = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        var bounds = image.ComputeOpaqueBounds(alphaThreshold);
        if (bounds.IsEmpty) return BoundingCircle.Empty;

        int xMin = (int)bounds.Min.X;
        int yMin = (int)bounds.Min.Y;
        int xMax = (int)bounds.Max.X;
        int yMax = (int)bounds.Max.Y;

        // Pass 1: find P1, the farthest opaque pixel from the rect
        // center. This becomes one end of the seed diameter.
        var seed = bounds.Center;
        Vector2 p1 = seed;
        float maxSq = -1f;
        for (int y = yMin; y < yMax; y++)
        {
            for (int x = xMin; x < xMax; x++)
            {
                if (image.GetPixel(x, y).A <= alphaThreshold) continue;
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var sq = Vector2.DistanceSquared(seed, p);
                if (sq > maxSq) { maxSq = sq; p1 = p; }
            }
        }

        // Pass 2: find P2, the farthest opaque pixel from P1. The
        // segment P1-P2 is the seed diameter for the bounding circle.
        Vector2 p2 = p1;
        maxSq = -1f;
        for (int y = yMin; y < yMax; y++)
        {
            for (int x = xMin; x < xMax; x++)
            {
                if (image.GetPixel(x, y).A <= alphaThreshold) continue;
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var sq = Vector2.DistanceSquared(p1, p);
                if (sq > maxSq) { maxSq = sq; p2 = p; }
            }
        }
        var center = (p1 + p2) * 0.5f;
        var radius = MathF.Sqrt(maxSq) * 0.5f;

        // Pass 3: Ritter expansion on pixel centers. For any pixel
        // still outside the current circle, grow + shift to include
        // it. After the loop, pad the radius by a half-pixel diagonal
        // so the full pixel (not just its center) is enclosed.
        for (int y = yMin; y < yMax; y++)
        {
            for (int x = xMin; x < xMax; x++)
            {
                if (image.GetPixel(x, y).A <= alphaThreshold) continue;
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var d = Vector2.Distance(center, p);
                if (d <= radius) continue;
                var newRadius = (radius + d) * 0.5f;
                center += (d - radius) / (2f * d) * (p - center);
                radius = newRadius;
            }
        }
        radius += MathF.Sqrt(0.5f);
        return new BoundingCircle(center, radius);
    }

    /// <summary>
    /// Computes a nominal set of axis-aligned bounding rectangles that cover every pixel.
    /// </summary>
    public static BoundingRect[] ComputeOpaqueRects(
        this Bitmap image,
        int cellSize = 8,
        byte alphaThreshold = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfLessThan(cellSize, 1);

        var (w, h) = image.Size;
        if (w <= 0 || h <= 0) return Array.Empty<BoundingRect>();

        int cols = (w + cellSize - 1) / cellSize;
        int rows = (h + cellSize - 1) / cellSize;

        // opaque[r * cols + c] is true if any pixel in cell (c, r) exceeds
        // the alpha threshold.
        var opaque = new bool[rows * cols];
        bool any = false;
        for (int y = 0; y < h; y++)
        {
            int r = y / cellSize;
            int rowBase = r * cols;
            for (int x = 0; x < w; x++)
            {
                if (image.GetPixel(x, y).A > alphaThreshold)
                {
                    int idx = rowBase + (x / cellSize);
                    if (!opaque[idx])
                    {
                        opaque[idx] = true;
                        any = true;
                    }
                }
            }
        }
        if (!any) return Array.Empty<BoundingRect>();

        // Greedy rectangle merge: scan cells row-major; for each opaque
        // cell not yet consumed, expand right while cells stay opaque,
        // then expand down while every cell across that horizontal span
        // stays opaque. Mark consumed cells and emit the rectangle.
        var consumed = new bool[opaque.Length];
        var rects = new List<BoundingRect>();
        for (int r = 0; r < rows; r++)
        {
            int rowBase = r * cols;
            for (int c = 0; c < cols; c++)
            {
                int idx = rowBase + c;
                if (!opaque[idx] || consumed[idx]) continue;

                int c1 = c;
                while (c1 + 1 < cols
                       && opaque[rowBase + c1 + 1]
                       && !consumed[rowBase + c1 + 1])
                {
                    c1++;
                }

                int r1 = r;
                while (r1 + 1 < rows && CanExtendDown(opaque, consumed, cols, c, c1, r1 + 1))
                {
                    r1++;
                }

                for (int rr = r; rr <= r1; rr++)
                {
                    int rb = rr * cols;
                    for (int cc = c; cc <= c1; cc++)
                        consumed[rb + cc] = true;
                }

                int minPx = c * cellSize;
                int minPy = r * cellSize;
                int maxPx = Math.Min((c1 + 1) * cellSize, w);
                int maxPy = Math.Min((r1 + 1) * cellSize, h);
                rects.Add(new BoundingRect(
                    new Vector2(minPx, minPy),
                    new Vector2(maxPx, maxPy)));
            }
        }

        return rects.ToArray();
    }

    private static bool CanExtendDown(bool[] opaque, bool[] consumed, int cols, int c0, int c1, int r)
    {
        int rb = r * cols;
        for (int c = c0; c <= c1; c++)
        {
            int i = rb + c;
            if (!opaque[i] || consumed[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// Picks a good default <see cref="HitShape"/> for the image's
    /// opaque pixels (in image-pixel space, origin top-left). Returns
    /// a <see cref="CircleHitShape"/> for round/blocky silhouettes and
    /// a <see cref="CapsuleHitShape"/> for elongated ones, choosing
    /// whichever covers the opaque pixels with the smaller area.
    /// </summary>
    public static HitShape ComputeOpaqueHitShape(this Bitmap image, byte alphaThreshold = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        var bounds = image.ComputeOpaqueBounds(alphaThreshold);
        if (bounds.IsEmpty) return HitShape.None;

        var circle = image.ComputeOpaqueCircle(alphaThreshold);

        var size = bounds.Size;
        bool tall = size.Y > size.X;
        float longSide = tall ? size.Y : size.X;
        float shortSide = tall ? size.X : size.Y;

        // For nearly-square silhouettes the inscribed capsule degenerates
        // to a circle. Skip the capsule fit and return the tight circle.
        if (longSide < shortSide * 1.4f)
            return new CircleHitShape(circle.Center, circle.Radius);

        // Start with the capsule inscribed in the AABB along the long
        // axis (endpoints offset from center by half-long - half-short).
        // Then widen the radius until every opaque pixel is inside.
        var axis = tall ? new Vector2(0, 1) : new Vector2(1, 0);
        var center = bounds.Center;
        float halfL = longSide * 0.5f - shortSide * 0.5f;
        var endA = center - axis * halfL;
        var endB = center + axis * halfL;
        float radius = shortSide * 0.5f;

        int xMin = (int)bounds.Min.X, yMin = (int)bounds.Min.Y;
        int xMax = (int)bounds.Max.X, yMax = (int)bounds.Max.Y;
        for (int y = yMin; y < yMax; y++)
        {
            for (int x = xMin; x < xMax; x++)
            {
                if (image.GetPixel(x, y).A <= alphaThreshold) continue;
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = DistancePointToSegment(p, endA, endB);
                if (d > radius) radius = d;
            }
        }
        // Half-pixel diagonal pad so the full pixel (not just its center) is enclosed.
        radius += MathF.Sqrt(0.5f);

        // Compare enclosing area; the circle already wraps every opaque
        // pixel (Ritter), so prefer it when the capsule isn't tighter.
        float circleArea = MathF.PI * circle.Radius * circle.Radius;
        float capArea = MathF.PI * radius * radius + 2f * radius * (halfL * 2f);
        return circleArea <= capArea
            ? new CircleHitShape(circle.Center, circle.Radius)
            : new CapsuleHitShape(endA, endB, radius);
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lenSq = ab.LengthSquared();
        if (lenSq <= float.Epsilon) return Vector2.Distance(p, a);
        var t = Vector2.Dot(p - a, ab) / lenSq;
        if (t < 0f) t = 0f;
        else if (t > 1f) t = 1f;
        return Vector2.Distance(p, a + t * ab);
    }
}
