namespace Blitter.Blocks3D;

/// <summary>
/// The voxel grid for a world.
/// </summary>
public interface IVoxelWorld
{
    /// <summary>
    /// Palette resolving every id returned by <see cref="GetVoxel"/>.
    /// </summary>
    VoxelPalette Palette { get; }

    /// <summary>
    /// Gets the voxel id at <paramref name="coord"/>.
    /// Out-of-bounds reads return <c>0</c> (air).
    /// </summary>
    int GetVoxel(VoxelCoord coord);

    /// <summary>
    /// Sets the voxel at <paramref name="coord"/>. 
    /// Out-of-bounds writes return <c>false</c>.
    /// </summary>
    bool SetVoxel(VoxelCoord coord, int id);

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

/// <summary>
/// Handler for <see cref="IVoxelWorld.VoxelsChanged"/>.
/// </summary>
public delegate void VoxelsChangedHandler(IVoxelWorld world, in VoxelBox change);

/// <summary>
/// A single voxel cell coordinate in world-voxel space (one unit per cell).
/// </summary>
public readonly record struct VoxelCoord(int X, int Y, int Z);

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

    /// <summary>True when this box and <paramref name="other"/> share at least one cell.</summary>
    public bool Intersects(in VoxelBox other) =>
        Min.X <= other.Max.X && Max.X >= other.Min.X &&
        Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
        Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
}
