namespace Blitter.Blocks3D;

/// <summary>
/// A stacked drawable layer in a <see cref="Scene3D"/>. Scenes update
/// every <see cref="Enabled"/> layer in list order each frame and draw
/// every <see cref="Visible"/> layer. Concrete layers manage their own
/// contents (see <see cref="PlayField3D"/> for sprites + barriers).
/// </summary>
public abstract class Layer3D : Entity, IDrawable3D, IUpdateEnabled
{
    /// <summary>When false the scene skips this layer's update.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When false the scene skips this layer's draw.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Render the layer's current state.</summary>
    public void Draw(Renderer3D renderer)
    {
        if (!Visible)
            return;
        DrawContent(renderer);
    }

    /// <summary>Render the layer's current state.</summary>
    protected abstract void DrawContent(Renderer3D renderer);
}
