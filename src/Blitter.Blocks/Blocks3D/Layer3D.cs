namespace Blitter.Blocks3D;

/// <summary>
/// A 3D drawable layer. Concrete layers manage their own contents (see
/// <see cref="PlayField3D"/> for sprites + barriers).
/// </summary>
public abstract class Layer3D : Entity, IDrawable3D
{
    /// <summary>Render the layer's current state.</summary>
    public void Draw(Renderer3D renderer)
    {
        DrawContent(renderer);
    }

    /// <summary>Render the layer's current state.</summary>
    protected abstract void DrawContent(Renderer3D renderer);
}
