namespace Blitter.Blocks3D;

/// <summary>
/// Ergonomic helpers around <see cref="IVoxelWorld"/> for content code
/// that prefers (x, y, z) coordinates. Hot-path code (meshers, collision)
/// should keep calling <see cref="IVoxelWorld.GetVoxel"/> /
/// <see cref="IVoxelWorld.SetVoxel"/> directly.
/// </summary>
public static class VoxelWorldExtensions
{
    /// <summary>Gets the voxel at (x, y, z).</summary>
    public static VoxelInfo GetVoxel(this IVoxelWorld world, int x, int y, int z) =>
        world.GetVoxel(new VoxelCoord(x, y, z));

    /// <summary>Sets the voxel at (x, y, z) to <paramref name="voxel"/>.</summary>
    public static bool SetVoxel(this IVoxelWorld world, int x, int y, int z, VoxelInfo voxel) =>
        world.SetVoxel(new VoxelCoord(x, y, z), voxel);

    /// <summary>Ensures the inclusive cell range is materialized.</summary>
    public static void EnsureVoxels(this IVoxelWorld world,
        int minX, int minY, int minZ, int maxX, int maxY, int maxZ) =>
        world.EnsureVoxels(new VoxelBox(minX, minY, minZ, maxX, maxY, maxZ));

    /// <summary>Releases storage for cells outside the inclusive range.</summary>
    public static void TrimVoxelsOutside(this IVoxelWorld world,
        int minX, int minY, int minZ, int maxX, int maxY, int maxZ) =>
        world.TrimVoxelsOutside(new VoxelBox(minX, minY, minZ, maxX, maxY, maxZ));

    /// <summary>Sets the cell to the type registered under <paramref name="typeName"/>.</summary>
    public static bool SetVoxel(this IVoxelWorld world, int x, int y, int z, string typeName) =>
        world.SetVoxel(new VoxelCoord(x, y, z), world.Catalog[typeName]);

    /// <summary>True when the cell at the given coordinates is air.</summary>
    public static bool IsAir(this IVoxelWorld world, int x, int y, int z) =>
        world.GetVoxel(new VoxelCoord(x, y, z)).IsAir;

    /// <summary>True when the cell at the given coordinates is opaque.</summary>
    public static bool IsOpaque(this IVoxelWorld world, int x, int y, int z) =>
        world.GetVoxel(new VoxelCoord(x, y, z)).IsOpaque;
}
