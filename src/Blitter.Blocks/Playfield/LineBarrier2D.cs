using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks;

/// <summary>
/// A barrier defined by a single line segment. Compose several to
/// build rectangles, maze walls, polylines, etc.
/// </summary>
public sealed class LineBarrier2D : Barrier2D
{
    public Vector2 Start { get; }
    public Vector2 End { get; }

    public LineBarrier2D(Vector2 start, Vector2 end)
    {
        Start = start;
        End = end;
    }

    public LineBarrier2D(float x1, float y1, float x2, float y2)
        : this(new Vector2(x1, y1), new Vector2(x2, y2))
    {
    }

    /// <summary>
    /// Builds the four edges of an axis-aligned rectangle as a single
    /// closed polyline of <see cref="LineBarrier2D"/>s, in clockwise
    /// order starting from the top-left.
    /// </summary>
    public static LineBarrier2D[] Rect(float x, float y, float width, float height, string? tag = null)
    {
        var x2 = x + width;
        var y2 = y + height;
        return
        [
            new LineBarrier2D(x,  y,  x2, y)  { Tag = tag },
            new LineBarrier2D(x2, y,  x2, y2) { Tag = tag },
            new LineBarrier2D(x2, y2, x,  y2) { Tag = tag },
            new LineBarrier2D(x,  y2, x,  y)  { Tag = tag },
        ];
    }

    public override bool Intersects(BoundingCircle circle)
    {
        if (circle.IsEmpty)
            return false;

        // Closest point on segment to circle center, then compare
        // squared distance against squared radius.
        var ab = End - Start;
        var lenSq = Vector2.Dot(ab, ab);
        Vector2 closest;
        if (lenSq <= float.Epsilon)
        {
            // Degenerate (zero-length) segment: just the start point.
            closest = Start;
        }
        else
        {
            var t = Vector2.Dot(circle.Center - Start, ab) / lenSq;
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            closest = Start + ab * t;
        }

        var d = circle.Center - closest;
        return Vector2.Dot(d, d) <= circle.Radius * circle.Radius;
    }
}
