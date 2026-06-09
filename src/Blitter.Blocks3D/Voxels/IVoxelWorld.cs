namespace Blitter.Blocks3D;

/// <summary>
/// The voxel grid for a world.
/// </summary>
public interface IVoxelWorld
{
    /// <summary>
    /// The catalog of voxel types this world can hold.
    /// </summary>
    VoxelCatalog Catalog { get; }

    /// <summary>
    /// Gets the voxel at <paramref name="coord"/>.
    /// Out-of-bounds reads return air.
    /// </summary>
    VoxelInfo GetVoxel(in VoxelCoord coord);

    /// <summary>
    /// Sets the voxel at <paramref name="coord"/> to <paramref name="voxel"/>.
    /// Out-of-bounds writes return <c>false</c>.
    /// </summary>
    bool SetVoxel(in VoxelCoord coord, in VoxelInfo voxel);

    /// <summary>
    /// Raised when one or more voxels change or when a region is first materialized.
    /// </summary>
    event VoxelsChangedHandler? VoxelsChanged;

    /// <summary>
    /// Ensures every voxel in the inclusive <paramref name="range"/> is ready to be accessed.
    /// This is primarily an affordance for chunked or streamed implementations to pre-load/pre-generate the region.
    /// </summary>
    void EnsureVoxels(in VoxelBox range);

    /// <summary>
    /// Tells the world that the voxels outside <paramref name="range"/> are not in current use
    /// and can be discarded/unloaded.
    /// </summary>
    void TrimVoxelsOutside(in VoxelBox range);
}
