using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// Local shape of a single voxel cell. Used by the mesher to emit
/// geometry and by the hit shape to build per-cell collision boxes.
/// </summary>
public enum VoxelShape
{
    /// <summary>Empty cell. Mesher emits nothing; collision ignores it.</summary>
    None,

    /// <summary>Full unit cube filling the cell.</summary>
    FullBlock,
}

/// <summary>
/// Static properties of one voxel kind. Looked up by id through a
/// <see cref="VoxelPalette"/>.
/// </summary>
public sealed class VoxelType
{
    /// <summary>The id this type is registered under in its palette.</summary>
    public int Id { get; init; }

    /// <summary>Human-readable name. Useful for debugging and content tooling.</summary>
    public string Name { get; init; } = "";

    /// <summary>True for cells that have no geometry and no collision (air, void).</summary>
    public bool IsAir { get; init; }

    /// <summary>True when the cell fully occludes its neighbors. Drives face culling during meshing.</summary>
    public bool IsOpaque { get; init; } = true;

    /// <summary>Local geometry of the cell.</summary>
    public VoxelShape Shape { get; init; } = VoxelShape.FullBlock;

    /// <summary>
    /// Texture mapping for this voxel's faces. A bare
    /// <see cref="Texture2D"/> assigned here implicitly becomes a
    /// <see cref="UniformVoxelTexture"/> (same picture on every face);
    /// use <see cref="TopSideBottomVoxelTexture"/> or
    /// <see cref="SixFaceVoxelTexture"/> for per-face variation. Null
    /// leaves the mesher to use a default / untextured material.
    /// </summary>
    public VoxelTexture? Texture { get; init; }

    /// <summary>
    /// Resolves the texture for one local face of this voxel.
    /// <paramref name="face"/> uses the mesher's index convention,
    /// matching <see cref="VoxelFace"/>: 0=-X, 1=+X, 2=-Y, 3=+Y,
    /// 4=-Z, 5=+Z. Returns null when this type has no texture.
    /// </summary>
    public Texture2D? GetFaceTexture(int face) => Texture?.GetFace((VoxelFace)face);

    /// <summary>Physics material the voxel reports when it acts as a collision surface.</summary>
    public PhysicsMaterial Physics { get; init; } = PhysicsMaterial.Ideal;

    /// <summary>The canonical "air" type. Id 0 in any default palette.</summary>
    public static readonly VoxelType Air = new()
    {
        Id = 0,
        Name = "air",
        IsAir = true,
        IsOpaque = false,
        Shape = VoxelShape.None,
    };
}
