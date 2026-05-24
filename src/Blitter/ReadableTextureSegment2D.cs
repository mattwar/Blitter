namespace Blitter;

/// <summary>
/// A <see cref="ReadableTexture2D"/> that exposes a sub-rectangle of
/// another readable texture as a standalone texture. <see cref="GetPixel"/>
/// reads from the source at the offset. Renderers recognize this type
/// (via <see cref="ITextureRegion"/>) and route draws accordingly.
/// </summary>
public sealed class ReadableTextureSegment2D : ReadableTexture2D, ITextureRegion
{
    public ReadableTextureSegment2D(ReadableTexture2D source, Rect sourceRect)
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
        SourceRect = sourceRect;
    }

    /// <summary>The backing texture this segment reads from.</summary>
    public ReadableTexture2D Source { get; }

    /// <inheritdoc/>
    public Rect SourceRect { get; }

    Texture2D ITextureRegion.Source => Source;

    /// <inheritdoc/>
    public override int Width => (int)SourceRect.Width;

    /// <inheritdoc/>
    public override int Height => (int)SourceRect.Height;

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
        Source.GetPixel((int)SourceRect.X + x, (int)SourceRect.Y + y);
}
