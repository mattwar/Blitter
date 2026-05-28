namespace Blitter;

/// <summary>
/// A <see cref="Texture2D"/> that is a rectangular region of another texture.
/// </summary>
public class TextureRegion2D : Texture2D, ITextureRegion
{
    public TextureRegion2D(Texture2D source, Rect region)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (region.Width <= 0 || region.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(region), "Segment must have positive size.");
        var (sw, sh) = source.Size;
        if (region.X < 0 || region.Y < 0
            || region.X + region.Width > sw
            || region.Y + region.Height > sh)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "Segment lies outside the source texture.");
        }
        Source = source;
        Region = region;
    }

    /// <inheritdoc/>
    public Texture2D Source { get; }

    /// <inheritdoc/>
    public Rect Region { get; }

    /// <inheritdoc/>
    public override int Width => (int)Region.Width;

    /// <inheritdoc/>
    public override int Height => (int)Region.Height;

    /// <inheritdoc/>
    public override PixelFormat PixelFormat => Source.PixelFormat;

    /// <inheritdoc/>
    public override int Version => Source.Version;

    /// <inheritdoc/>
    public override int LevelCount => Source.LevelCount;

    /// <inheritdoc/>
    public override bool Mipmaps => Source.Mipmaps;

    /// <inheritdoc/>
    public override bool IsDisposed => Source.IsDisposed;

    /// <inheritdoc/>
    public override void Invalidate() => Source.Invalidate();

    /// <inheritdoc/>
    public override void Dispose() { /* doesn't own source */ }

    /// <inheritdoc/>
    public override Texture2D Slice(Rect region) =>
        new TextureRegion2D(Source, OffsetSlice(Region, region, nameof(region)));

    internal static Rect OffsetSlice(Rect outer, Rect inner, string paramName)
    {
        if (inner.Width <= 0 || inner.Height <= 0)
            throw new ArgumentOutOfRangeException(paramName, "Segment must have positive size.");
        if (inner.X < 0 || inner.Y < 0
            || inner.X + inner.Width > outer.Width
            || inner.Y + inner.Height > outer.Height)
        {
            throw new ArgumentOutOfRangeException(paramName, "Segment lies outside this region.");
        }
        return new Rect(outer.X + inner.X, outer.Y + inner.Y, inner.Width, inner.Height);
    }

    /// <summary>
    /// If <paramref name="texture"/> is an <see cref="ITextureRegion"/>, 
    /// replaces it with the region's <see cref="ITextureRegion.Source"/> 
    /// and offsets <paramref name="texture"/> into the source's pixel space.
    /// </summary>
    public static void Unwrap(ref Texture2D texture, ref Rect rect)
    {
        if (texture is ITextureRegion region)
        {
            var r = region.Region;
            texture = region.Source;
            rect = new Rect(rect.X + r.X, rect.Y + r.Y, r.Width, r.Height);
        }
    }
}
