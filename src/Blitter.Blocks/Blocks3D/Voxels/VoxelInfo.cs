namespace Blitter.Blocks3D;

/// <summary>
/// A read of one voxel: the <see cref="VoxelType"/> stored there and the
/// handful of properties hot-path callers need without a second lookup.
/// The default value (and any out-of-bounds read) is air.
/// </summary>
public readonly struct VoxelInfo
{
    private readonly VoxelType? _type;

    /// <summary>Wraps <paramref name="type"/>.</summary>
    public VoxelInfo(VoxelType type) => _type = type;

    /// <summary>Wraps a <see cref="VoxelType"/> so it can be passed where a <see cref="VoxelInfo"/> is expected.</summary>
    public static implicit operator VoxelInfo(VoxelType type) => new(type);

    /// <summary>The voxel type at this cell; <see cref="VoxelType.Air"/> when empty.</summary>
    public VoxelType Type => _type ?? VoxelType.Air;

    /// <summary>True when the cell has no geometry or collision.</summary>
    public bool IsAir => Type.IsAir;

    /// <summary>True when the cell fully occludes its neighbors.</summary>
    public bool IsOpaque => Type.IsOpaque;
}
