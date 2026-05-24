using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks;

/// <summary>
/// A barrier defined by a single line segment. Compose several to
/// build rectangles, maze walls, polylines, etc.
/// </summary>
/// <remarks>
/// Each segment has an outward <see cref="Normal"/> (the half-space the
/// barrier protects) and an optional <see cref="OneSided"/> flag for
/// jump-through floors. Use the named factories (<see cref="Floor"/>,
/// <see cref="Ceiling"/>, <see cref="WallLeft"/>, <see cref="WallRight"/>,
/// <see cref="Slope"/>, <see cref="Rect"/>) so you don't have to think
/// about winding.
/// </remarks>
public class LineBarrier2D : Barrier2D
{
    public Vector2 Start { get; }
    public Vector2 End { get; }

    /// <summary>
    /// Unit vector perpendicular to the segment, pointing toward the
    /// "solid-free" side. Hit handlers classify floor / wall / ceiling
    /// by inspecting this.
    /// </summary>
    public Vector2 Normal { get; }

    /// <summary>
    /// When true, sprites only collide when their center is on the
    /// <see cref="Normal"/> side of the segment. Use for jump-through
    /// platforms. Fast-moving sprites can still tunnel through; that's
    /// caller's problem to solve (substep, max fall speed, etc.).
    /// </summary>
    public bool OneSided { get; init; }

    /// <summary>
    /// Creates a segment with an outward normal computed from the winding:
    /// for clockwise loops in screen-space (Y-down) the normal points
    /// outward. For freestanding segments prefer the named factories.
    /// </summary>
    public LineBarrier2D(Vector2 start, Vector2 end)
        : this(start, end, DefaultNormal(start, end))
    {
    }

    public LineBarrier2D(float x1, float y1, float x2, float y2)
        : this(new Vector2(x1, y1), new Vector2(x2, y2))
    {
    }

    /// <summary>
    /// Creates a segment with an explicit outward normal. <paramref name="normal"/>
    /// is normalized; a zero vector falls back to the winding-derived normal.
    /// </summary>
    public LineBarrier2D(Vector2 start, Vector2 end, Vector2 normal)
    {
        Start = start;
        End = end;
        var lenSq = normal.LengthSquared();
        Normal = lenSq > float.Epsilon
            ? normal / MathF.Sqrt(lenSq)
            : DefaultNormal(start, end);
    }

    // Perpendicular of (End-Start) that points "outward" for a
    // clockwise-wound loop in screen-space (Y-down): rotate the
    // direction -90° visually.
    private static Vector2 DefaultNormal(Vector2 start, Vector2 end)
    {
        var d = end - start;
        var lenSq = d.LengthSquared();
        if (lenSq <= float.Epsilon)
            return Vector2.UnitY * -1f;
        var perp = new Vector2(d.Y, -d.X);
        return perp / MathF.Sqrt(lenSq);
    }

    /// <summary>
    /// Horizontal floor at <paramref name="y"/> from <paramref name="xLeft"/>
    /// to <paramref name="xRight"/>. Normal points up (toward -Y).
    /// </summary>
    public static LineBarrier2D Floor(float xLeft, float xRight, float y, bool oneSided = false)
        => new(new Vector2(xLeft, y), new Vector2(xRight, y), new Vector2(0f, -1f))
        {
            OneSided = oneSided,
        };

    /// <summary>
    /// Horizontal ceiling at <paramref name="y"/>. Normal points down (toward +Y).
    /// </summary>
    public static LineBarrier2D Ceiling(float xLeft, float xRight, float y)
        => new(new Vector2(xLeft, y), new Vector2(xRight, y), new Vector2(0f, 1f));

    /// <summary>
    /// Vertical wall at <paramref name="x"/> with sprites expected to stay on
    /// the left of it. Normal points -X.
    /// </summary>
    public static LineBarrier2D WallLeft(float x, float yTop, float yBottom)
        => new(new Vector2(x, yTop), new Vector2(x, yBottom), new Vector2(-1f, 0f));

    /// <summary>
    /// Vertical wall at <paramref name="x"/> with sprites expected to stay on
    /// the right of it. Normal points +X.
    /// </summary>
    public static LineBarrier2D WallRight(float x, float yTop, float yBottom)
        => new(new Vector2(x, yTop), new Vector2(x, yBottom), new Vector2(1f, 0f));

    /// <summary>
    /// Arbitrary segment whose outward normal is whichever of the two
    /// perpendiculars points toward <paramref name="solidFreeSide"/>.
    /// </summary>
    public static LineBarrier2D Slope(Vector2 start, Vector2 end, Vector2 solidFreeSide, bool oneSided = false)
    {
        var d = end - start;
        var perp = new Vector2(-d.Y, d.X);
        if (Vector2.Dot(perp, solidFreeSide) < 0f)
            perp = -perp;
        return new LineBarrier2D(start, end, perp)
        {
            OneSided = oneSided,
        };
    }

    /// <summary>
    /// Builds the four edges of an axis-aligned rectangle as a single
    /// closed polyline of <see cref="LineBarrier2D"/>s, wound clockwise
    /// starting from the top-left so each segment's normal points outward.
    /// </summary>
    public static LineBarrier2D[] Rect(float x, float y, float width, float height)
    {
        var x2 = x + width;
        var y2 = y + height;
        return
        [
            new LineBarrier2D(new Vector2(x,  y ), new Vector2(x2, y )),
            new LineBarrier2D(new Vector2(x2, y ), new Vector2(x2, y2)),
            new LineBarrier2D(new Vector2(x2, y2), new Vector2(x,  y2)),
            new LineBarrier2D(new Vector2(x,  y2), new Vector2(x,  y )),
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
        if (Vector2.Dot(d, d) > circle.Radius * circle.Radius)
            return false;

        if (OneSided)
        {
            // Only collide when the sprite center is on the solid-free
            // side of the segment (or exactly on it). Lets things drop
            // through the bottom of jump-through platforms.
            if (Vector2.Dot(circle.Center - Start, Normal) < 0f)
                return false;
        }
        return true;
    }
}

