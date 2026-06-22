namespace Blitter.Blocks3D;

/// <summary>
/// Populates a region of voxels on demand.
/// </summary>
public interface IVoxelGenerator
{
    /// <summary>
    /// Fills the <see cref="VoxelBuffer"/> with voxels. 
    /// Iterate <see cref="VoxelBuffer.Bounds"/> and assign voxels by their coordinate: <c>voxels[x, y, z] = type</c>. 
    /// The buffer arrives cleared to air; only non-air voxels need to be written.
    /// </summary>
    void Generate(in VoxelBuffer voxels);
}
