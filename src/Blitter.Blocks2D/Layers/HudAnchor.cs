using Blitter.Bits;
using System.Numerics;

namespace Blitter.Blocks2D;

/// <summary>
/// Where on the screen a HUD element anchors itself. Pairs with an
/// offset to position the element relative to one of nine common
/// points around the viewport.
/// </summary>
public enum HudAnchor
{
    TopLeft,    Top,    TopRight,
    Left,       Center, Right,
    BottomLeft, Bottom, BottomRight,
}

internal static class HudAnchorExtensions
{
    /// <summary>
    /// Returns the screen-space position of the given anchor inside a
    /// viewport of <paramref name="viewport"/>, optionally offset by
    /// <paramref name="elementSize"/> so the anchor refers to the
    /// matching corner/edge of an element (e.g. TopRight anchors a text
    /// block by its top-right corner instead of its top-left origin).
    /// </summary>
    public static Vector2 ResolveOrigin(this HudAnchor anchor, Vector2 viewport, Vector2 elementSize, Vector2 offset)
    {
        float ox = anchor switch
        {
            HudAnchor.TopLeft or HudAnchor.Left or HudAnchor.BottomLeft => offset.X,
            HudAnchor.Top or HudAnchor.Center or HudAnchor.Bottom => (viewport.X - elementSize.X) * 0.5f + offset.X,
            _ => viewport.X - elementSize.X - offset.X,
        };
        float oy = anchor switch
        {
            HudAnchor.TopLeft or HudAnchor.Top or HudAnchor.TopRight => offset.Y,
            HudAnchor.Left or HudAnchor.Center or HudAnchor.Right => (viewport.Y - elementSize.Y) * 0.5f + offset.Y,
            _ => viewport.Y - elementSize.Y - offset.Y,
        };
        return new Vector2(ox, oy);
    }
}
