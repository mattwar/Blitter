namespace Blitter;

/// <summary>
/// A base type for any 2D texture. 
/// </summary>
public abstract class Texture2D : Texture, IDisposable
{
    /// <summary>
    /// Width of the texture in pixels.
    /// </summary>
    public abstract int Width { get; }

    /// <summary>
    /// Height of the texture in pixels.
    /// </summary>
    public abstract int Height { get; }

    /// <summary>
    /// Pixel format of the texture.
    /// </summary>
    public abstract PixelFormat PixelFormat { get; }

    /// <summary>
    /// Bumped each time the texture's contents change. 
    /// Renderers use this to detect when their cached GPU upload is stale.
    /// </summary>
    public abstract int Version { get; }

    /// <summary>
    /// Number of mip levels stored. 
    /// <c>1</c> means just a base level.
    /// </summary>
    public abstract int LevelCount { get; }

    /// <summary>
    /// Hints to renderers that a mip chain should be generated for this texture on upload. 
    /// Ignored when <see cref="LevelCount"/> is already greater than 1.
    /// </summary>
    public abstract bool Mipmaps { get; }

    /// <summary>
    /// Whether the texture has been disposed.
    /// </summary>
    public abstract bool IsDisposed { get; }

    /// <summary>
    /// Marks the texture's contents as changed so renderers re-upload.
    /// </summary>
    public abstract void Invalidate();

    /// <inheritdoc/>
    public abstract void Dispose();

    /// <summary>
    /// Size of the texture in pixels.
    /// </summary>
    public (int Width, int Height) Size => (Width, Height);

    /// <summary>
    /// Returns a rectangular region of this texture.
    /// </summary>
    public virtual Texture2D Slice(Rect region) => 
        new TextureRegion2D(this, region);
}
