namespace Blitter;

/// <summary>
/// A <see cref="ReadableTexture2D"/> that is a rectangular region of another readable texture. 
/// </summary>
public sealed class ReadableTextureRegion2D : ReadableTexture2D, ITextureRegion
{
    public ReadableTextureRegion2D(ReadableTexture2D source, Rect sourceRect)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceRect), "Segment must have positive size.");
        var (sw, sh) = source.Size;
        if (sourceRect.X < 0 || sourceRect.Y < 0
            || sourceRect.X + sourceRect.Width > sw
            || sourceRect.Y + sourceRect.Height > sh)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRect), "Segment lies outside the source texture.");
        }
        Source = source;
        Region = sourceRect;
    }

    /// <summary>
    /// The backing texture this segment reads from.
    /// </summary>
    public ReadableTexture2D Source { get; }

    /// <inheritdoc/>
    public Rect Region { get; }

    Texture2D ITextureRegion.Source => Source;

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
    public override Color GetPixel(int x, int y) =>
        Source.GetPixel((int)Region.X + x, (int)Region.Y + y);
}
