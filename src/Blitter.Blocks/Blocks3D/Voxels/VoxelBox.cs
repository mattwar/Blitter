namespace Blitter.Blocks3D;

/// <summary>
/// An inclusive box of voxel cells
/// </summary>
public readonly record struct VoxelBox(VoxelCoord Min, VoxelCoord Max)
{
    /// <summary>Builds a box from inclusive min/max cell components.</summary>
    public VoxelBox(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
        : this(new VoxelCoord(minX, minY, minZ), new VoxelCoord(maxX, maxY, maxZ)) { }

    /// <summary>A degenerate box covering the single cell <paramref name="coord"/>.</summary>
    public static VoxelBox Single(VoxelCoord coord) => new(coord, coord);

    /// <summary>A degenerate box covering the single cell (x, y, z).</summary>
    public static VoxelBox Single(int x, int y, int z) =>
        new(new VoxelCoord(x, y, z), new VoxelCoord(x, y, z));

    /// <summary>The number of voxels the box covers (inclusive on both ends).</summary>
    public int Volume =>
        (Max.X - Min.X + 1) * (Max.Y - Min.Y + 1) * (Max.Z - Min.Z + 1);

    /// <summary>True when this box and <paramref name="other"/> share at least one cell.</summary>
    public bool Intersects(in VoxelBox other) =>
        Min.X <= other.Max.X && Max.X >= other.Min.X &&
        Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
        Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
}
