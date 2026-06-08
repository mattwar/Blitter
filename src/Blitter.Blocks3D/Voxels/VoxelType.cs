using Blitter.Bits;

namespace Blitter.Blocks3D;

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

    /// <summary>
    /// Geometry of the cell — how it meshes and how it collides. Defaults
    /// to a textureless full cube (<see cref="CubeVoxelShape.Untextured"/>).
    /// Assign a <see cref="CubeVoxelShape"/> carrying a
    /// <see cref="VoxelTexture"/> for a textured block, or
    /// <see cref="EmptyVoxelShape.Instance"/> for air.
    /// </summary>
    public VoxelShape Shape { get; init; } = CubeVoxelShape.Untextured;

    /// <summary>Physics material the voxel reports when it acts as a collision surface.</summary>
    public PhysicsMaterial Physics { get; init; } = PhysicsMaterial.Ideal;

    /// <summary>The canonical "air" type. Id 0 in any default palette.</summary>
    public static readonly VoxelType Air = new()
    {
        Id = 0,
        Name = "air",
        IsAir = true,
        IsOpaque = false,
        Shape = EmptyVoxelShape.Instance,
    };
}
