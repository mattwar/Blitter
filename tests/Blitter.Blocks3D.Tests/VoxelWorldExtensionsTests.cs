namespace Blitter.Tests;

public class VoxelWorldExtensionsTests
{
    private static (ArrayVoxelWorld world, VoxelType stone) MakeWorld()
    {
        var palette = new VoxelPalette();
        var stone = palette.Add(new VoxelType { Id = 1, Name = "stone", IsOpaque = true });
        return (new ArrayVoxelWorld(4, 4, 4, palette), stone);
    }

    [Fact]
    public void GetVoxelType_ResolvesThroughPalette()
    {
        var (world, stone) = MakeWorld();
        world.SetVoxel(1, 1, 1, 1);

        Assert.Same(stone, world.GetVoxelType(1, 1, 1));
        Assert.Same(VoxelType.Air, world.GetVoxelType(0, 0, 0));
    }

    [Fact]
    public void SetVoxel_ByType_WritesTypeId()
    {
        var (world, stone) = MakeWorld();
        Assert.True(world.SetVoxel(2, 2, 2, stone));
        Assert.Equal(1, world.GetVoxel(2, 2, 2));
    }

    [Fact]
    public void SetVoxel_ByName_ResolvesId()
    {
        var (world, _) = MakeWorld();
        Assert.True(world.SetVoxel(0, 0, 0, "stone"));
        Assert.Equal(1, world.GetVoxel(0, 0, 0));
    }

    [Fact]
    public void IsAir_And_IsOpaque_Delegate()
    {
        var (world, _) = MakeWorld();
        world.SetVoxel(1, 1, 1, 1);

        Assert.True(world.IsAir(0, 0, 0));
        Assert.False(world.IsAir(1, 1, 1));
        Assert.True(world.IsOpaque(1, 1, 1));
        Assert.False(world.IsOpaque(0, 0, 0));
    }
}
