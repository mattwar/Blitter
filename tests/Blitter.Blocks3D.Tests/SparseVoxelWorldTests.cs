namespace Blitter.Tests;

public class SparseVoxelWorldTests
{
    /// <summary>Records the min-corner of each region it is asked to generate and optionally seeds a constant type.</summary>
    private sealed class RecordingGenerator : IVoxelGenerator
    {
        public List<VoxelCoord> Generated { get; } = new();
        public VoxelType? Fill { get; init; }

        public void Generate(in VoxelBuffer cells)
        {
            var b = cells.Bounds;
            Generated.Add(b.Min);
            if (Fill is not null)
            {
                for (int z = b.Min.Z; z <= b.Max.Z; z++)
                for (int y = b.Min.Y; y <= b.Max.Y; y++)
                for (int x = b.Min.X; x <= b.Max.X; x++)
                    cells[x, y, z] = new VoxelInfo(Fill);
            }
        }
    }

    private static VoxelCatalog MakeCatalog()
    {
        var catalog = new VoxelCatalog();
        catalog.Add(new VoxelType { Name = "stone" });
        return catalog;
    }

    [Fact]
    public void Constructor_RejectsNonPositiveChunkSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SparseVoxelWorld(MakeCatalog(), new RecordingGenerator(), new ChunkSize(0, 16, 16)));
    }

    [Fact]
    public void GetVoxel_OnUnloadedChunk_ReturnsAirWithoutGenerating()
    {
        var catalog = MakeCatalog();
        var gen = new RecordingGenerator { Fill = catalog["stone"] };
        var world = new SparseVoxelWorld(catalog, gen);

        Assert.True(world.GetVoxel(5, 5, 5).IsAir);
        Assert.Empty(gen.Generated); // reads must not force generation
    }

    [Fact]
    public void SetVoxel_GeneratesChunkAndStores()
    {
        var gen = new RecordingGenerator();
        var world = new SparseVoxelWorld(MakeCatalog(), gen, new ChunkSize(16, 64, 16));
        var stone = world.Catalog["stone"];

        Assert.True(world.SetVoxel(3, 2, 1, stone));
        Assert.Same(stone, world.GetVoxel(3, 2, 1).Type);
        Assert.Single(gen.Generated);
    }

    [Fact]
    public void WorldToChunk_UsesFlooredDivisionForNegatives()
    {
        var world = new SparseVoxelWorld(MakeCatalog(), new RecordingGenerator(), new ChunkSize(16, 16, 16));

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
        var world = new SparseVoxelWorld(MakeCatalog(), gen);
        var coord = new ChunkCoord(2, 0, -1);

        Assert.True(world.EnsureChunk(coord));
        Assert.False(world.EnsureChunk(coord));
        Assert.Single(gen.Generated);
        // Default chunk size is 16x64x16, so chunk (2, 0, -1) starts at world (32, 0, -16).
        Assert.Equal(new VoxelCoord(32, 0, -16), gen.Generated[0]);
        Assert.True(world.IsChunkLoaded(coord));
    }

    [Fact]
    public void UnloadChunk_RemovesData()
    {
        var catalog = MakeCatalog();
        var gen = new RecordingGenerator { Fill = catalog["stone"] };
        var world = new SparseVoxelWorld(catalog, gen);
        var coord = new ChunkCoord(0, 0, 0);

        world.EnsureChunk(coord);
        Assert.True(world.UnloadChunk(coord));
        Assert.False(world.IsChunkLoaded(coord));
        Assert.False(world.UnloadChunk(coord)); // already gone
        Assert.True(world.GetVoxel(0, 0, 0).IsAir); // reads air again
    }
}
