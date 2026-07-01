namespace Blitter.Blocks2D;

/// <summary>
/// Prepares renderer state before an entity or subtree is drawn.
/// </summary>
public interface IDrawableSetup2D
{
    void Setup(Renderer2D renderer);
}