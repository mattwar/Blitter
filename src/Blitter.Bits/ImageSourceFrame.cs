namespace Blitter.Bits;

/// <summary>
/// Declarative description of a single frame within an
/// <see cref="ImageSourceState"/>: a tile selected from a grid sheet, with an
/// optional per-frame flip. <see cref="FilePath"/> and <see cref="TileSize"/>
/// are inherited from the enclosing state (and in turn the root
/// <see cref="ImageSource"/>) when left unset, so a sheet declared once at the
/// top flows down to every frame. A bare <c>(Column, Row)</c> tuple or a path
/// string converts implicitly, so frame lists read tersely
/// (<c>Frames = { (1, 0), (2, 0) }</c>).
/// </summary>
public sealed class ImageSourceFrame
{
    /// <summary>
    /// Path to the image file. Inherited from the enclosing state/source when
    /// unset.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// An already-built texture to use directly, the escape hatch for when you
    /// have the exact image in hand. Mutually exclusive with <see cref="FilePath"/>,
    /// <see cref="Tile"/>, and <see cref="Index"/>; a bare <see cref="Texture2D"/>
    /// or <see cref="AnimationFrame"/> converts implicitly.
    /// </summary>
    public Texture2D? Texture { get; set; }

    /// <summary>
    /// The zero-based <c>(Column, Row)</c> of the tile to select. Requires an
    /// effective <see cref="TileSize"/> (own or inherited). Mutually exclusive
    /// with <see cref="Index"/>; leave both unset to use the whole image.
    /// </summary>
    public (int Column, int Row)? Tile { get; set; }

    /// <summary>
    /// The zero-based index of the frame, the 1D alternative to <see cref="Tile"/>.
    /// With an effective <see cref="TileSize"/> it is the Nth grid cell
    /// (row-major); without one it is the Nth auto-sensed region of the
    /// inherited sheet. Mutually exclusive with <see cref="Tile"/>.
    /// </summary>
    public int? Index { get; set; }

    /// <summary>
    /// The size, in pixels, of each grid cell. Inherited from the enclosing
    /// state/source when unset.
    /// </summary>
    public (int Width, int Height)? TileSize { get; set; }

    /// <summary>
    /// Mirror applied to this frame, composed (XOR) with any flip inherited
    /// from the enclosing state.
    /// </summary>
    public FlipMode Flip { get; set; }

    /// <summary>Treats a bare path string as a whole-image frame.</summary>
    public static implicit operator ImageSourceFrame(string path) =>
        new() { FilePath = path };

    /// <summary>Treats a bare <c>(Column, Row)</c> tuple as a tile selection.</summary>
    public static implicit operator ImageSourceFrame((int Column, int Row) tile) =>
        new() { Tile = tile };

    /// <summary>Treats a bare integer as an <see cref="Index"/> selection.</summary>
    public static implicit operator ImageSourceFrame(int index) =>
        new() { Index = index };

    /// <summary>Treats an already-built texture as an explicit frame.</summary>
    public static implicit operator ImageSourceFrame(Texture2D texture) =>
        new() { Texture = texture };

    /// <summary>Treats an already-built animation frame as an explicit frame.</summary>
    public static implicit operator ImageSourceFrame(AnimationFrame frame) =>
        new() { Texture = frame.Texture, Flip = frame.Flip };

    // Resolves against the inherited context (file path, tile size, flip),
    // overriding with this frame's own values where set.
    internal AnimationFrame Resolve(in ImageSourceContext context)
    {
        var ctx = context.Inherit(FilePath, TileSize, Flip);
        if (Texture is not null)
        {
            if (FilePath is not null || Tile is not null || Index is not null)
                throw new InvalidOperationException(
                    $"An {nameof(ImageSourceFrame)} sets either an explicit {nameof(Texture)} or a {nameof(FilePath)}/{nameof(Tile)}/{nameof(Index)}, not both.");
            return new AnimationFrame(Texture, ctx.Flip);
        }
        var texture = ImageSource.ResolveTexture(ctx.FilePath, Tile, Index, ctx.TileSize);
        return new AnimationFrame(texture, ctx.Flip);
    }
}
