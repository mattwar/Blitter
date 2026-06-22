using System.Numerics;

namespace Blitter.Tests;

/// <summary>
/// Covers chunk pooling in <see cref="VoxelChunkSource3D"/>: chunks evicted by
/// <see cref="ChunkSource3D.TrimChunksOutside"/> are retained and recycled onto
/// new coords on a later load, reusing the chunk structure, its barrier, and
/// the mesh/collision buffers in place rather than reallocating.
/// </summary>
public class VoxelChunkPoolTests
{
    private static VoxelCatalog MakeCatalog()
    {
        var catalog = new VoxelCatalog();
        catalog.Add(new VoxelType { Name = "stone" });
        return catalog;
    }

    private static VoxelChunkSource3D MakeSource() =>
        new(new ArrayVoxelWorld(32, 32, 32, MakeCatalog()), Vector3.One, 4, 4, 4);

    // Evicts every loaded chunk by trimming to a far-away single-chunk box.
    private static void EvictAll(VoxelChunkSource3D source) =>
        source.TrimChunksOutside(new ChunkCoord(100, 100, 100), new ChunkCoord(100, 100, 100));

    [Fact]
    public void EvictedChunk_IsRecycled_OnNextLoad()
    {
        var source = MakeSource();
        var first = (Chunk3D)source.GetChunk(new ChunkCoord(0, 0, 0))!;

        EvictAll(source);
        var second = (Chunk3D)source.GetChunk(new ChunkCoord(1, 0, 0))!;

        Assert.Same(first, second);
    }

    [Fact]
    public void Recycled_ReusesSameBarrierAndGrid()
    {
        var source = MakeSource();
        var first = (Chunk3D)source.GetChunk(new ChunkCoord(0, 0, 0))!;
        var firstBarrier = (VoxelChunkBarrier3D)first.Barriers[0];
        var firstGrid = firstBarrier.Grid;

        EvictAll(source);
        var second = (Chunk3D)source.GetChunk(new ChunkCoord(1, 0, 0))!;
        var secondBarrier = (VoxelChunkBarrier3D)second.Barriers[0];

        Assert.Same(firstBarrier, secondBarrier);
        Assert.Same(firstGrid, secondBarrier.Grid);
    }

    [Fact]
    public void Recycled_RetargetsGridToNewCoord()
    {
        var source = MakeSource();
        source.GetChunk(new ChunkCoord(0, 0, 0));

        EvictAll(source);
        var second = (Chunk3D)source.GetChunk(new ChunkCoord(2, 1, 3))!;
        var grid = ((VoxelChunkBarrier3D)second.Barriers[0]).Grid;

        Assert.Equal(new ChunkCoord(2, 1, 3), grid.Coord);
        Assert.Equal(new ChunkCoord(2, 1, 3), second.Coord);
        Assert.Equal(8, grid.OriginCellX);  // 2 * 4
        Assert.Equal(4, grid.OriginCellY);  // 1 * 4
        Assert.Equal(12, grid.OriginCellZ); // 3 * 4
    }

    [Fact]
    public void Recycled_ResetsGridVersion()
    {
        var source = MakeSource();
        var first = (Chunk3D)source.GetChunk(new ChunkCoord(0, 0, 0))!;
        var grid = ((VoxelChunkBarrier3D)first.Barriers[0]).Grid;
        grid.BumpVersion();
        grid.BumpVersion();
        Assert.Equal(2, grid.Version);

        EvictAll(source);
        source.GetChunk(new ChunkCoord(1, 0, 0));

        Assert.Equal(0, grid.Version);
    }

    [Fact]
    public void Recycled_RepositionsBarrierToNewOrigin()
    {
        var source = MakeSource();
        source.GetChunk(new ChunkCoord(0, 0, 0));

        EvictAll(source);
        var second = (Chunk3D)source.GetChunk(new ChunkCoord(2, 0, 1))!;
        var barrier = (VoxelChunkBarrier3D)second.Barriers[0];

        Assert.Equal(barrier.Grid.WorldOrigin, barrier.Position);
        Assert.Equal(new Vector3(8, 0, 4), barrier.Position);
    }

    [Fact]
    public void Recycled_VisualReMeshesNewData()
    {
        var source = MakeSource();
        var first = (Chunk3D)source.GetChunk(new ChunkCoord(0, 0, 0))!;
        var visual = ((VoxelChunkBarrier3D)first.Barriers[0]).VoxelVisual;
        visual.Rebuild();
        Assert.False(visual.NeedsRebuild());

        EvictAll(source);
        var second = (Chunk3D)source.GetChunk(new ChunkCoord(1, 0, 0))!;
        var reusedVisual = ((VoxelChunkBarrier3D)second.Barriers[0]).VoxelVisual;

        // Same visual instance, but marked stale so it re-meshes the
        // recycled grid's data on the next draw.
        Assert.Same(visual, reusedVisual);
        Assert.True(reusedVisual.NeedsRebuild());
    }

    [Fact]
    public void NoEviction_GeneratesFreshChunks()
    {
        var source = MakeSource();
        var a = source.GetChunk(new ChunkCoord(0, 0, 0));
        var b = source.GetChunk(new ChunkCoord(1, 0, 0));

        Assert.NotSame(a, b);
    }

    [Fact]
    public void RecycledChunk_HasNoLeftoverSprites()
    {
        var source = MakeSource();
        var first = (Chunk3D)source.GetChunk(new ChunkCoord(0, 0, 0))!;
        first.AddSprite(new Sprite3D());
        Assert.Single(first.Sprites);

        EvictAll(source);
        var second = (Chunk3D)source.GetChunk(new ChunkCoord(1, 0, 0))!;

        Assert.Empty(second.Sprites);
    }
}
