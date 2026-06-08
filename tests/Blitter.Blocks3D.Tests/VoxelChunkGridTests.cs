using System.Numerics;

namespace Blitter.Tests;

public class VoxelChunkGridTests
{
    private static (ArrayVoxelWorld world, VoxelPalette palette) MakeWorld(int w = 32, int h = 32, int d = 32)
    {
        var palette = new VoxelPalette();
        palette.Add(new VoxelType { Id = 1, Name = "stone" });
        return (new ArrayVoxelWorld(w, h, d, palette), palette);
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
        var (world, _) = MakeWorld();
        world.SetVoxel(17, 1, 1, 1); // world coord
        var grid = new VoxelChunkGrid(world, new ChunkCoord(1, 0, 0), 16, 16, 16, Vector3.One);

        // Local (1,1,1) maps to world (16+1, 1, 1) = (17,1,1).
        Assert.Equal(1, grid.GetVoxel(1, 1, 1));
        Assert.Equal(0, grid.GetVoxel(0, 1, 1));
    }

    [Fact]
    public void Palette_ForwardsFromWorld()
    {
        var (world, palette) = MakeWorld();
        var grid = new VoxelChunkGrid(world, default, 16, 16, 16, Vector3.One);
        Assert.Same(palette, grid.Palette);
    }
}
