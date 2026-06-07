namespace Blitter.Tests;

public class SparseVoxelWorldTests
{
    /// <summary>Records the coords it is asked to generate and optionally seeds a constant id.</summary>
    private sealed class RecordingGenerator : IVoxelGenerator
    {
        public List<ChunkCoord> Generated { get; } = new();
        public int FillId { get; init; }

        public void Generate(ChunkCoord coord, int cellsX, int cellsY, int cellsZ, int[] cells)
        {
            Generated.Add(coord);
            if (FillId != 0)
                Array.Fill(cells, FillId);
        }
    }

    private static VoxelPalette MakePalette()
    {
        var palette = new VoxelPalette();
        palette.Add(new VoxelType { Id = 1, Name = "stone" });
        return palette;
    }

    [Fact]
    public void Constructor_RejectsNonPositiveChunkSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SparseVoxelWorld(MakePalette(), new RecordingGenerator(), 0, 16, 16));
    }

    [Fact]
    public void GetVoxel_OnUnloadedChunk_ReturnsAirWithoutGenerating()
    {
        var gen = new RecordingGenerator { FillId = 1 };
        var world = new SparseVoxelWorld(MakePalette(), gen);

        Assert.Equal(0, world.GetVoxel(5, 5, 5));
        Assert.Empty(gen.Generated); // reads must not force generation
    }

    [Fact]
    public void SetVoxel_GeneratesChunkAndStores()
    {
        var gen = new RecordingGenerator();
        var world = new SparseVoxelWorld(MakePalette(), gen, 16, 64, 16);

        Assert.True(world.SetVoxel(3, 2, 1, 1));
        Assert.Equal(1, world.GetVoxel(3, 2, 1));
        Assert.Single(gen.Generated);
    }

    [Fact]
    public void WorldToChunk_UsesFlooredDivisionForNegatives()
    {
        var world = new SparseVoxelWorld(MakePalette(), new RecordingGenerator(), 16, 16, 16);

        Assert.Equal(new ChunkCoord(0, 0, 0), world.WorldToChunk(0, 0, 0));
        Assert.Equal(new ChunkCoord(0, 0, 0), world.WorldToChunk(15, 15, 15));
        Assert.Equal(new ChunkCoord(1, 0, 0), world.WorldToChunk(16, 0, 0));
        Assert.Equal(new ChunkCoord(-1, -1, -1), world.WorldToChunk(-1, -1, -1));
        Assert.Equal(new ChunkCoord(-1, 0, 0), world.WorldToChunk(-16, 0, 0));
        Assert.Equal(new ChunkCoord(-2, 0, 0), world.WorldToChunk(-17, 0, 0));
    }

    [Fact]
    public void EnsureChunk_GeneratesOnceThenReportsAlreadyLoaded()
    {
        var gen = new RecordingGenerator();
        var world = new SparseVoxelWorld(MakePalette(), gen);
        var coord = new ChunkCoord(2, 0, -1);

        Assert.True(world.EnsureChunk(coord));
        Assert.False(world.EnsureChunk(coord));
        Assert.Single(gen.Generated);
        Assert.Equal(coord, gen.Generated[0]);
        Assert.True(world.IsChunkLoaded(coord));
    }

    [Fact]
    public void UnloadChunk_RemovesData()
    {
        var gen = new RecordingGenerator { FillId = 1 };
        var world = new SparseVoxelWorld(MakePalette(), gen);
        var coord = new ChunkCoord(0, 0, 0);

        world.EnsureChunk(coord);
        Assert.True(world.UnloadChunk(coord));
        Assert.False(world.IsChunkLoaded(coord));
        Assert.False(world.UnloadChunk(coord)); // already gone
        Assert.Equal(0, world.GetVoxel(0, 0, 0)); // reads air again
    }
}
