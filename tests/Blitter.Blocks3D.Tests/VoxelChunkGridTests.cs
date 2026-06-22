using System.Numerics;

namespace Blitter.Tests;

public class VoxelChunkGridTests
{
    private static (ArrayVoxelWorld world, VoxelCatalog catalog) MakeWorld(int w = 32, int h = 32, int d = 32)
    {
        var catalog = new VoxelCatalog();
        catalog.Add(new VoxelType { Name = "stone" });
        return (new ArrayVoxelWorld(w, h, d, catalog), catalog);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCellCounts()
    {
        var (world, _) = MakeWorld();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VoxelChunkGrid(world, default, 0, 16, 16, Vector3.One));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCellSize()
    {
        var (world, _) = MakeWorld();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VoxelChunkGrid(world, default, 16, 16, 16, new Vector3(1f, 0f, 1f)));
    }

    [Fact]
    public void Origin_IsChunkCoordTimesCellCount()
    {
        var (world, _) = MakeWorld();
        var grid = new VoxelChunkGrid(world, new ChunkCoord(1, 0, 1), 16, 16, 16, Vector3.One);

        Assert.Equal(16, grid.OriginCellX);
        Assert.Equal(0, grid.OriginCellY);
        Assert.Equal(16, grid.OriginCellZ);
    }

    [Fact]
    public void WorldOrigin_ScalesByCellSize()
    {
        var (world, _) = MakeWorld();
        var cellSize = new Vector3(2f, 3f, 4f);
        var grid = new VoxelChunkGrid(world, new ChunkCoord(1, 1, 1), 8, 8, 8, cellSize);

        Assert.Equal(new Vector3(16f, 24f, 32f), grid.WorldOrigin);
    }

    [Fact]
    public void GetVoxel_ForwardsToWorldWithOriginOffset()
    {
        var (world, catalog) = MakeWorld();
        world.SetVoxel(17, 1, 1, catalog["stone"]); // world coord
        var grid = new VoxelChunkGrid(world, new ChunkCoord(1, 0, 0), 16, 16, 16, Vector3.One);

        // Local (1,1,1) maps to world (16+1, 1, 1) = (17,1,1).
        Assert.Same(catalog["stone"], grid.GetVoxel(1, 1, 1).Type);
        Assert.True(grid.GetVoxel(0, 1, 1).IsAir);
    }

    [Fact]
    public void Catalog_ForwardsFromWorld()
    {
        var (world, catalog) = MakeWorld();
        var grid = new VoxelChunkGrid(world, default, 16, 16, 16, Vector3.One);
        Assert.Same(catalog, grid.Catalog);
    }
}
