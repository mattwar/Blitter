namespace Blitter;

/// <summary>
/// A base class for any cubemap-shaped texture. 
/// </summary>
public abstract class TextureCube : Texture
{
    /// <summary>
    /// Edge length of every face's base mip level, in pixels. 
    /// All six faces are square and share this size.
    /// </summary>
    public abstract int Size { get; }

    /// <summary>
    /// Pixel format shared by all six faces.
    /// </summary>
    public abstract PixelFormat Format { get; }

    /// <summary>
    /// Number of mip levels stored per face. 
    /// <c>1</c> means only a base level exists.
    /// </summary>
    public abstract int LevelCount { get; }

    /// <summary>
    /// Hints to renderers that a mip chain should be auto-generated for this cubemap on upload. 
    /// Ignored when <see cref="LevelCount"/> is already greater than 1.
    /// </summary>
    public abstract bool Mipmaps { get; }

    /// <summary>
    /// Bumped each time the cubemap's contents change. 
    /// Renderers use this to detect when their cached GPU upload is stale.
    /// </summary>
    public abstract int Version { get; }

    /// <summary>
    /// Marks the cubemap contents as changed so renderers re-upload on the next draw.
    /// </summary>
    public abstract void Invalidate();
}
