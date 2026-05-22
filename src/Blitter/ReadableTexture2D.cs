namespace Blitter;

/// <summary>
/// A <see cref="Texture2D"/> whose pixels can be read on the CPU.
/// </summary>
public abstract class ReadableTexture2D : Texture2D, IReadableTexture2D
{
    /// <summary>Returns the color of the pixel at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public abstract Color GetPixel(int x, int y);

    /// <inheritdoc/>
    public override ReadableTexture2D Slice(Rect region) => new ReadableTextureSegment2D(this, region);
}
