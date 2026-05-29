namespace Blitter.Blocks3D;

/// <summary>
/// The source-of-truth voxel grid for a world. Implementations decide
/// how cells are stored — flat array, chunked dictionary, sparse octree,
/// streamed from disk, etc. Coordinates are world-voxel coordinates
/// (each unit is one cell), not chunk-relative.
/// </summary>
public interface IVoxelWorld
{
    /// <summary>Palette resolving every id returned by <see cref="GetVoxel"/>.</summary>
    VoxelPalette Palette { get; }

    /// <summary>
    /// Voxel id at <paramref name="x"/>, <paramref name="y"/>,
    /// <paramref name="z"/>. Out-of-bounds reads return
    /// <c>0</c> (air) so meshers and collision can probe neighbor
    /// cells without bounds checking.
    /// </summary>
    int GetVoxel(int x, int y, int z);

    /// <summary>
    /// Sets the voxel at <paramref name="x"/>, <paramref name="y"/>,
    /// <paramref name="z"/> to <paramref name="id"/>. 
    /// Returns <c>true</c> if the cell actually changed. Out-of-bounds writes
    /// return <c>false</c>.
    /// </summary>
    bool SetVoxel(int x, int y, int z, int id);

    /// <summary>
    /// Raised when one or more cells change. 
    /// Subscribers (typically <c>VoxelChunkSource3D</c>) mark affected chunks dirty.
    /// The <see cref="VoxelChangeEventArgs"/> reports a bounding box of
    /// changed cells so subscribers can compute which chunks to
    /// invalidate without per-cell overhead during bulk edits.
    /// </summary>
    event EventHandler<VoxelChangeEventArgs>? VoxelsChanged;
}

/// <summary>
/// Inclusive integer bounding box of changed voxel cells.
/// </summary>
public sealed class VoxelChangeEventArgs : EventArgs
{
    public int MinX { get; }
    public int MinY { get; }
    public int MinZ { get; }
    public int MaxX { get; }
    public int MaxY { get; }
    public int MaxZ { get; }

    public VoxelChangeEventArgs(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
    {
        MinX = minX; MinY = minY; MinZ = minZ;
        MaxX = maxX; MaxY = maxY; MaxZ = maxZ;
    }

    /// <summary>Convenience for the common single-cell case.</summary>
    public static VoxelChangeEventArgs Single(int x, int y, int z) =>
        new(x, y, z, x, y, z);
}
