namespace Blitter;

/// <summary>
/// A <see cref="Texture2D"/> that exposes a sub-rectangle of another
/// texture as a standalone texture. Renderers recognize this type and
/// route draws to <see cref="Source"/> with an offset source rect.
/// </summary>
public class TextureSegment2D : Texture2D, ITextureRegion
{
    public TextureSegment2D(Texture2D source, Rect sourceRect)
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

    /// <inheritdoc/>
    public Texture2D Source { get; }

    /// <inheritdoc/>
    public Rect SourceRect { get; }

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

    /// <summary>
    /// If <paramref name="image"/> is an <see cref="ITextureRegion"/>, replaces it with the
    /// region's <see cref="ITextureRegion.Source"/> and offsets <paramref name="source"/>
    /// into the source's pixel space.
    /// </summary>
    public static void Unwrap(ref Texture2D image, ref Rect source)
    {
        if (image is ITextureRegion region)
        {
            var rect = region.SourceRect;
            image = region.Source;
            source = new Rect(rect.X + source.X, rect.Y + source.Y, source.Width, source.Height);
        }
    }
}
