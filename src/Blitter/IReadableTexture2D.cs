namespace Blitter;

/// <summary>
/// A 2D image whose pixels can be read on the CPU. Implemented by the
/// <see cref="ReadableTexture2D"/> base class and by
/// <see cref="ReadableTextureSegment2D"/>.
/// </summary>
public interface IReadableTexture2D
{
    /// <summary>Width of the image in pixels.</summary>
    int Width { get; }

    /// <summary>Height of the image in pixels.</summary>
    int Height { get; }

    /// <summary>Returns the color of the pixel at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    Color GetPixel(int x, int y);
}
