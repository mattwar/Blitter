namespace Blitter.Blocks2D;

/// <summary>
/// Attaches a <see cref="Camera2D"/> capability to an entity and optionally
/// applies it to the renderer when drawn.
/// </summary>
public sealed class AttachedCamera2D : Behavior, IDrawable2D, ICamera2D
{
    /// <inheritdoc/>
    public string? Name { get; set; }

    /// <summary>The camera provided by this behavior.</summary>
    public Camera2D Camera { get; set; } = new();

    /// <summary>When true, drawing this behavior assigns <see cref="Camera"/> to the renderer.</summary>
    public bool Enabled { get; set; } = true;

    /// <inheritdoc/>
    public void Draw(Renderer2D renderer)
    {
        if (Enabled)
            renderer.Camera = Camera;
    }
}