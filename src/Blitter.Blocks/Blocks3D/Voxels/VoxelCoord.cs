namespace Blitter.Blocks3D;

/// <summary>
/// A single voxel cell coordinate in world-voxel space (one unit per cell).
/// </summary>
public readonly record struct VoxelCoord(int X, int Y, int Z);
