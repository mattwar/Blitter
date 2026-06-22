using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// Static properties of one voxel kind. Registered in a
/// <see cref="VoxelCatalog"/> and resolved by <see cref="Name"/>.
/// </summary>
public sealed class VoxelType
{
    /// <summary>
    /// Dense storage index stamped by the <see cref="VoxelCatalog"/> this
    /// type is added to. An implementation detail of how worlds pack their
    /// voxels; not part of a type's identity.
    /// </summary>
    internal int Id { get; set; }

    /// <summary>The catalog this type belongs to, or <c>null</c> until it is added.</summary>
    internal VoxelCatalog? Owner { get; set; }

    /// <summary>
    /// Intrinsic identity of the voxel kind. Required and unique within a
    /// catalog; content and tooling refer to voxels by this name.
    /// </summary>
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

    /// <summary>The canonical "air" type. Index 0 in every catalog.</summary>
    public static readonly VoxelType Air = new()
    {
        Id = 0,
        Name = "air",
        IsAir = true,
        IsOpaque = false,
        Shape = EmptyVoxelShape.Instance,
    };
}
