namespace Blitter.Blocks3D;

/// <summary>
/// Ergonomic helpers around <see cref="IVoxelWorld"/> for content code
/// that prefers <see cref="VoxelType"/> over raw ids. Hot-path code
/// (meshers, collision) should keep calling
/// <see cref="IVoxelWorld.GetVoxel"/> / <see cref="IVoxelWorld.SetVoxel"/>
/// directly to avoid per-cell palette lookups.
/// </summary>
public static class VoxelWorldExtensions
{
    /// <summary>Resolves the cell's id through <see cref="IVoxelWorld.Palette"/>.</summary>
    public static VoxelType GetVoxelType(this IVoxelWorld world, int x, int y, int z) =>
        world.Palette[world.GetVoxel(x, y, z)];

    /// <summary>Sets the cell to <paramref name="type"/>.<see cref="VoxelType.Id"/>.</summary>
    public static bool SetVoxel(this IVoxelWorld world, int x, int y, int z, VoxelType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return world.SetVoxel(x, y, z, type.Id);
    }

    /// <summary>Sets the cell to the id of the type registered under <paramref name="typeName"/>.</summary>
    public static bool SetVoxel(this IVoxelWorld world, int x, int y, int z, string typeName) =>
        world.SetVoxel(x, y, z, world.Palette.IdOf(typeName));

    /// <summary>True when the cell at the given coordinates is air.</summary>
    public static bool IsAir(this IVoxelWorld world, int x, int y, int z) =>
        world.Palette.IsAir(world.GetVoxel(x, y, z));

    /// <summary>True when the cell at the given coordinates is opaque.</summary>
    public static bool IsOpaque(this IVoxelWorld world, int x, int y, int z) =>
        world.Palette.IsOpaque(world.GetVoxel(x, y, z));
}
