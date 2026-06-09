namespace Blitter.Tests;

public class VoxelWorldExtensionsTests
{
    private static (ArrayVoxelWorld world, VoxelType stone) MakeWorld()
    {
        var catalog = new VoxelCatalog();
        var stone = catalog.Add(new VoxelType { Name = "stone", IsOpaque = true });
        return (new ArrayVoxelWorld(4, 4, 4, catalog), stone);
    }

    [Fact]
    public void GetVoxel_ResolvesType()
    {
        var (world, stone) = MakeWorld();
        world.SetVoxel(1, 1, 1, stone);

        Assert.Same(stone, world.GetVoxel(1, 1, 1).Type);
        Assert.Same(VoxelType.Air, world.GetVoxel(0, 0, 0).Type);
    }

    [Fact]
    public void SetVoxel_ByType_WritesType()
    {
        var (world, stone) = MakeWorld();
        Assert.True(world.SetVoxel(2, 2, 2, stone));
        Assert.Same(stone, world.GetVoxel(2, 2, 2).Type);
    }

    [Fact]
    public void SetVoxel_ByName_ResolvesType()
    {
        var (world, stone) = MakeWorld();
        Assert.True(world.SetVoxel(0, 0, 0, "stone"));
        Assert.Same(stone, world.GetVoxel(0, 0, 0).Type);
    }

    [Fact]
    public void IsAir_And_IsOpaque_Delegate()
    {
        var (world, stone) = MakeWorld();
        world.SetVoxel(1, 1, 1, stone);

        Assert.True(world.IsAir(0, 0, 0));
        Assert.False(world.IsAir(1, 1, 1));
        Assert.True(world.IsOpaque(1, 1, 1));
        Assert.False(world.IsOpaque(0, 0, 0));
    }
}
