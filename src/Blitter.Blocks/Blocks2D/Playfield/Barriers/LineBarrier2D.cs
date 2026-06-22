using System.Numerics;


namespace Blitter.Blocks2D;

/// <summary>
/// A barrier defined by a single line segment. Compose several to
/// build rectangles, maze walls, polylines, etc. Bounces sprites from
/// either side by default; set <see cref="OneSided"/> to restrict
/// collisions to one side (jump-through floors, one-way kickers, etc.).
/// </summary>
public class LineBarrier2D : Barrier2D
{
    public Vector2 Start { get; }
    public Vector2 End { get; }

    /// <summary>
    /// Unit vector perpendicular to the segment, derived from the
    /// winding (<see cref="Start"/> → <see cref="End"/>). Walk the
    /// segment from start to end: <c>Normal</c> points to your left.
    /// Only consulted when <see cref="OneSided"/> is true or by
    /// user code that wants a stable "front-side" classifier; the
    /// bounce itself uses the contact direction at collision time.
    /// May be overridden via object initializer.
    /// </summary>
    public Vector2 Normal { get; init; }

    /// <summary>
    /// When true, sprites only collide when their center is on the
    /// <see cref="Normal"/> side of the segment. The bouncing side
    /// is determined by winding: walking from <see cref="Start"/> to
    /// <see cref="End"/>, sprites collide on your left. If a one-sided
    /// barrier bounces from the wrong side, swap the endpoints.
    /// </summary>
    public bool OneSided { get; set; }

    /// <summary>
    /// Creates a two-sided segment between <paramref name="start"/>
    /// and <paramref name="end"/>.
    /// </summary>
    public LineBarrier2D(Vector2 start, Vector2 end)
    {
        Start = start;
        End = end;
        Normal = DefaultNormal(start, end);
    }

    public LineBarrier2D(float x1, float y1, float x2, float y2)
        : this(new Vector2(x1, y1), new Vector2(x2, y2))
    {
    }

    // Perpendicular of (End-Start) rotated so that, walking from start
    // to end in screen-space (Y-down), the result points to your left.
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
    /// to <paramref name="xRight"/>. Normal points up.
    /// </summary>
    public static LineBarrier2D Floor(float xLeft, float xRight, float y, bool oneSided = false)
        => new(new Vector2(xLeft, y), new Vector2(xRight, y))
        {
            OneSided = oneSided,
        };

    /// <summary>
    /// Horizontal ceiling at <paramref name="y"/>. Normal points down.
    /// </summary>
    public static LineBarrier2D Ceiling(float xLeft, float xRight, float y)
        => new(new Vector2(xRight, y), new Vector2(xLeft, y));

    /// <summary>
    /// Vertical wall at <paramref name="x"/> with sprites expected to stay on
    /// the left of it. Normal points -X.
    /// </summary>
    public static LineBarrier2D WallLeft(float x, float yTop, float yBottom)
        => new(new Vector2(x, yBottom), new Vector2(x, yTop));

    /// <summary>
    /// Vertical wall at <paramref name="x"/> with sprites expected to stay on
    /// the right of it. Normal points +X.
    /// </summary>
    public static LineBarrier2D WallRight(float x, float yTop, float yBottom)
        => new(new Vector2(x, yTop), new Vector2(x, yBottom));

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

    public override PosedHitShape2D HitShape =>
        new(new SegmentHitShape2D(Start, End, OneSided), Pose2D.Identity);
}

