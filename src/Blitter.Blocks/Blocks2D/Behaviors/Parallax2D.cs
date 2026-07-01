namespace Blitter.Blocks2D;

using System.Numerics;

/// <summary>
/// Applies a parallax-adjusted camera before an entity or subtree is drawn.
/// </summary>
public sealed class Parallax2D : Behavior, IDrawableSetup2D
{
    /// <summary>
    /// Per-axis parallax factor applied to the active camera. <c>(1, 1)</c>
    /// moves with the foreground, <c>(0, 0)</c> is locked to the screen,
    /// values in between drift, and values greater than one move faster than
    /// the foreground.
    /// </summary>
    public Vector2 Factor { get; set; } = Vector2.One;

    /// <inheritdoc/>
    public void Setup(Renderer2D renderer)
    {
        if (renderer.Camera is not { } camera || Factor == Vector2.One)
            return;

        renderer.Camera = new Camera2D
        {
            Position = camera.Position * Factor,
            Zoom = camera.Zoom,
        };
    }
}