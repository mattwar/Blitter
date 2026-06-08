namespace Blitter.Blocks3D;

/// <summary>
/// Populates one chunk's worth of voxels on demand. Used by
/// <see cref="SparseVoxelWorld"/> to fabricate a chunk the first time
/// any voxel within it is read.
/// </summary>
public interface IVoxelGenerator
{
    /// <summary>
    /// Fills <paramref name="voxels"/> with the voxel ids for the chunk
    /// at <paramref name="coord"/>. Indexing is row-major with X
    /// fastest, then Y, then Z: <c>voxels[(z * voxelsY + y) * voxelsX + x]</c>.
    /// World-voxel coordinates of local voxel (lx, ly, lz) within this
    /// chunk are <c>(coord.X*voxelsX + lx, coord.Y*voxelsY + ly, coord.Z*voxelsZ + lz)</c>.
    /// </summary>
    void Generate(ChunkCoord coord, int voxelsX, int voxelsY, int voxelsZ, int[] voxels);
}
