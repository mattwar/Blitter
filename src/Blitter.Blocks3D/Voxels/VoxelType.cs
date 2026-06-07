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
    /// Texture sampled for every face of this voxel unless one of the
    /// per-face overrides is set. Use a <see cref="TextureRegion2D"/>
    /// to share an atlas across types. Null leaves the mesher to use a
    /// default / untextured material.
    /// </summary>
    public Texture2D? Texture { get; init; }

    /// <summary>Override texture for the +Y (top) face. Null falls back through the resolution chain.</summary>
    public Texture2D? TopTexture { get; init; }

    /// <summary>Override texture for the -Y (bottom) face. Null falls back through the resolution chain.</summary>
    public Texture2D? BottomTexture { get; init; }

    /// <summary>Override texture shared by the four side faces (±X, ±Z). Null falls back through the resolution chain.</summary>
    public Texture2D? SideTexture { get; init; }

    /// <summary>
    /// Resolves the texture for one face of this voxel.
    /// <paramref name="face"/> uses the mesher's index convention:
    /// 0=-X, 1=+X, 2=-Y, 3=+Y, 4=-Z, 5=+Z. If only one of
    /// <see cref="Texture"/>, <see cref="TopTexture"/>, <see cref="BottomTexture"/>,
    /// or <see cref="SideTexture"/> is set, every face resolves to it;
    /// for per-face variation, set each side explicitly.
    /// </summary>
    public Texture2D? GetFaceTexture(int face)
    {
        // Any non-null property acts as the fallback for unset faces,
        // so a voxel with only TopTexture set looks identical on every
        // face. Order: prefer Texture, then Side, then Top, then Bottom.
        var fallback = Texture ?? SideTexture ?? TopTexture ?? BottomTexture;
        return face switch
        {
            3 => TopTexture    ?? fallback,
            2 => BottomTexture ?? fallback,
            _ => SideTexture   ?? fallback,
        };
    }

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
