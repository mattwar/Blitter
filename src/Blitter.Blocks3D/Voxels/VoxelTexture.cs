using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// Maps each face of a voxel to the texture drawn on it. This is cube
/// vocabulary: it answers "which picture goes on each of the six flat
/// faces." Shapes that are not six-sided cubes carry their UVs in their
/// own geometry instead and do not use this type.
/// </summary>
/// <remarks>
/// Faces are addressed in the voxel's own local space via
/// <see cref="VoxelFace"/>; per-cell orientation (when present) is
/// resolved by the mesher before the lookup, so a texture never needs
/// to know how its voxel is turned.
/// </remarks>
public abstract class VoxelTexture
{
    /// <summary>
    /// The texture for <paramref name="face"/>, or <c>null</c> to leave
    /// that face untextured (the mesher picks a default material).
    /// </summary>
    public abstract Texture2D? GetFace(VoxelFace face);

    /// <summary>
    /// Wraps a single <see cref="Texture2D"/> as a
    /// <see cref="UniformVoxelTexture"/> so a texture can be assigned
    /// directly to a <see cref="VoxelTexture"/>-typed property.
    /// </summary>
    public static implicit operator VoxelTexture(Texture2D texture) =>
        new UniformVoxelTexture(texture);
}

/// <summary>
/// The same texture on all six faces.
/// </summary>
public sealed class UniformVoxelTexture : VoxelTexture
{
    private readonly Texture2D? _texture;

    /// <summary>Creates a uniform texture from <paramref name="texture"/>.</summary>
    public UniformVoxelTexture(Texture2D? texture) => _texture = texture;

    /// <inheritdoc/>
    public override Texture2D? GetFace(VoxelFace face) => _texture;
}

/// <summary>
/// One texture for the top (+Y), one for the bottom (-Y), and one
/// shared by the four sides (±X, ±Z). The classic grass-block layout.
/// </summary>
public sealed class TopSideBottomVoxelTexture : VoxelTexture
{
    private readonly Texture2D? _top;
    private readonly Texture2D? _side;
    private readonly Texture2D? _bottom;

    /// <summary>
    /// Creates a top/side/bottom texture. A null <paramref name="bottom"/>
    /// falls back to <paramref name="side"/>; a null <paramref name="top"/>
    /// likewise falls back to <paramref name="side"/>.
    /// </summary>
    public TopSideBottomVoxelTexture(Texture2D? top, Texture2D? side, Texture2D? bottom = null)
    {
        _top = top;
        _side = side;
        _bottom = bottom;
    }

    /// <inheritdoc/>
    public override Texture2D? GetFace(VoxelFace face) => face switch
    {
        VoxelFace.PositiveY => _top ?? _side,
        VoxelFace.NegativeY => _bottom ?? _side,
        _ => _side,
    };
}

/// <summary>
/// A separately declared texture for every one of the six faces.
/// </summary>
public sealed class SixFaceVoxelTexture : VoxelTexture
{
    private readonly Texture2D? _negX;
    private readonly Texture2D? _posX;
    private readonly Texture2D? _negY;
    private readonly Texture2D? _posY;
    private readonly Texture2D? _negZ;
    private readonly Texture2D? _posZ;

    /// <summary>Creates a six-face texture, one entry per <see cref="VoxelFace"/>.</summary>
    public SixFaceVoxelTexture(
        Texture2D? negativeX,
        Texture2D? positiveX,
        Texture2D? negativeY,
        Texture2D? positiveY,
        Texture2D? negativeZ,
        Texture2D? positiveZ)
    {
        _negX = negativeX;
        _posX = positiveX;
        _negY = negativeY;
        _posY = positiveY;
        _negZ = negativeZ;
        _posZ = positiveZ;
    }

    /// <inheritdoc/>
    public override Texture2D? GetFace(VoxelFace face) => face switch
    {
        VoxelFace.NegativeX => _negX,
        VoxelFace.PositiveX => _posX,
        VoxelFace.NegativeY => _negY,
        VoxelFace.PositiveY => _posY,
        VoxelFace.NegativeZ => _negZ,
        VoxelFace.PositiveZ => _posZ,
        _ => null,
    };
}
