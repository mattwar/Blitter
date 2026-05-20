using System.Numerics;

namespace Blitter;

/// <summary>
/// A 2D camera describing which slice of world space is visible.
/// When assigned to <see cref="Renderer2D.Camera"/>, the renderer
/// maps world coordinates to the screen as
/// <c>(world - Position) * Zoom + viewportCenter</c>.
/// </summary>
public class Camera2D
{
    /// <summary>
    /// World-space point that appears at the center of the viewport.
    /// Defaults to <see cref="Vector2.Zero"/>.
    /// </summary>
    public Vector2 Position { get; set; } = Vector2.Zero;

    /// <summary>
    /// Uniform zoom factor. <c>1</c> = world units map 1:1 to viewport
    /// pixels; <c>2</c> draws everything twice as large; <c>0.5</c>
    /// zooms out. Must be positive.
    /// </summary>
    public float Zoom { get; set; } = 1f;
}
