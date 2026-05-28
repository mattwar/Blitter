namespace Blitter;

/// <summary>
/// A texture that exposes a sub-rectangle of another texture.
/// Renderers recognize this and route draws to <see cref="Source"/>
/// with an offset source rect.
/// </summary>
public interface ITextureRegion
{
    /// <summary>
    /// The backing texture this region reads from.
    /// </summary>
    Texture2D Source { get; }

    /// <summary>
    /// Sub-rectangle of <see cref="Source"/> exposed by this region, in source pixel coordinates.
    /// </summary>
    Rect Region { get; }
}
