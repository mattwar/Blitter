namespace Blitter;

/// <summary>
/// Implemented by objects that issue draws against a 2D renderer.
/// </summary>
public interface IDrawable2D
{
    void Draw(Renderer2D renderer);
}

/// <summary>
/// Implemented by objects that issue draws against a 3D renderer. 
/// </summary>
public interface IDrawable3D
{
    void Draw(Renderer3D renderer);
}
