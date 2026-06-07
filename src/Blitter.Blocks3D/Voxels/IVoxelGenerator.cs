namespace Blitter.Blocks3D;

/// <summary>
/// Populates one chunk's worth of voxel cells on demand. Used by
/// <see cref="SparseVoxelWorld"/> to fabricate a chunk the first time
/// any cell within it is read.
/// </summary>
public interface IVoxelGenerator
{
    /// <summary>
    /// Fills <paramref name="cells"/> with the voxel ids for the chunk
    /// at <paramref name="coord"/>. Indexing is row-major with X
    /// fastest, then Y, then Z: <c>cells[(z * cellsY + y) * cellsX + x]</c>.
    /// World-voxel coordinates of local cell (lx, ly, lz) within this
    /// chunk are <c>(coord.X*cellsX + lx, coord.Y*cellsY + ly, coord.Z*cellsZ + lz)</c>.
    /// </summary>
    void Generate(ChunkCoord coord, int cellsX, int cellsY, int cellsZ, int[] cells);
}
