namespace Blitter.Blocks2D;

/// <summary>
/// Draws the nearest <see cref="Bounds2D"/> rectangle as a world-space outline.
/// </summary>
public sealed class DrawWorldBounds2D : Behavior, IDrawable2D
{
    private Bounds2D? _bounds;

    /// <summary>Outline color.</summary>
    public Color Color { get; set; } = new Color(0, 200, 255, 255);

    public void Draw(Renderer2D renderer)
    {
        _bounds ??= this.Entity.TryFindTrait<Bounds2D>(out var found) ? found : null;
        if (_bounds?.Rect is not Rect bounds || bounds.Width <= 0f || bounds.Height <= 0f)
            return;

        using var _ = renderer.PushState();
        renderer.DrawColor = Color;
        var inset = 1f / (renderer.Camera?.Zoom ?? 1f);
        var x0 = bounds.X;
        var y0 = bounds.Y;
        var x1 = bounds.X + bounds.Width - inset;
        var y1 = bounds.Y + bounds.Height - inset;
        renderer.DrawLine(x0, y0, x1, y0);
        renderer.DrawLine(x1, y0, x1, y1);
        renderer.DrawLine(x1, y1, x0, y1);
        renderer.DrawLine(x0, y1, x0, y0);
    }
}