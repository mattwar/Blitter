namespace Blitter.Blocks2D;

/// <summary>
/// A non-visual layer whose sole responsibility is to install a
/// <see cref="Camera2D"/> onto the renderer for the rest of the scene to
/// draw through. Declaring the camera as a layer keeps it part of the
/// scene tree (rather than an imperative <c>renderer.Camera = …</c> call),
/// so it can be positioned, named, and resolved like any other node.
/// </summary>
/// <remarks>
/// Place this before world layers in <see cref="IContainer.Entities"/>: the camera is
/// assigned when this layer draws, so every layer composited after it sees
/// the camera that same frame. The <see cref="Camera2D"/> instance is stable
/// — only its <see cref="Camera2D.Position"/> mutates — so a behaviour such
/// as <c>CameraFollow2D</c> can drive the same instance each tick.
/// </remarks>
public sealed class CameraLayer2D : Layer2D, IUpdatable
{
    /// <summary>The camera installed onto the renderer when this layer draws.</summary>
    public Camera2D Camera { get; set; } = new();

    /// <inheritdoc/>
    public void Update(in EntityUpdateContext context)
    {
        // Pure configuration layer: nothing to advance.
    }

    /// <inheritdoc/>
    protected override void DrawContent(Renderer2D renderer)
    {
        renderer.Camera = Camera;
    }
}
