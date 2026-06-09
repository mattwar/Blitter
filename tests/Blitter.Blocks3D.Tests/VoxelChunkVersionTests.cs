using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

/// <summary>
/// Covers the notification-based invalidation: the world announces voxel
/// changes as ranges via <see cref="IVoxelWorld.VoxelsChanged"/>; the
/// <see cref="VoxelChunkSource3D"/> translates each range into version
/// bumps on the playfield chunks that read those cells. Visuals and hit
/// shapes poll their own <see cref="VoxelChunkGrid.Version"/>, so the
/// long-lived world roots none of them.
/// </summary>
public class VoxelChunkVersionTests
{
    private sealed class AirGenerator : IVoxelGenerator
    {
        public void Generate(in VoxelBuffer cells)
        {
            // leave as air
        }
    }

    private static VoxelCatalog MakeCatalog()
    {
        var catalog = new VoxelCatalog();
        catalog.Add(new VoxelType { Name = "stone" });
        return catalog;
    }

    // ---- World range materialize / trim ------------------------------

    [Fact]
    public void SparseWorld_EnsureVoxels_GeneratesEveryCoveringChunk()
    {
        // x 0..7 spans world chunks 0 and 1; chunk 2 (x 8..11) is untouched.
        var world = new SparseVoxelWorld(MakeCatalog(), new AirGenerator(), new ChunkSize(4, 4, 4));

        world.EnsureVoxels(0, 0, 0, 7, 0, 0);

        Assert.True(world.IsChunkLoaded(new ChunkCoord(0, 0, 0)));
        Assert.True(world.IsChunkLoaded(new ChunkCoord(1, 0, 0)));
        Assert.False(world.IsChunkLoaded(new ChunkCoord(2, 0, 0)));
    }

    [Fact]
    public void SparseWorld_Generation_AnnouncesWholeChunkRange()
    {
        var world = new SparseVoxelWorld(MakeCatalog(), new AirGenerator(), new ChunkSize(4, 4, 4));
        var events = new List<VoxelBox>();
        world.VoxelsChanged += (IVoxelWorld _, in VoxelBox e) => events.Add(e);

        world.EnsureVoxels(0, 0, 0, 3, 0, 0); // one chunk

        var e = Assert.Single(events);
        Assert.Equal(new VoxelCoord(0, 0, 0), e.Min);
        Assert.Equal(new VoxelCoord(3, 3, 3), e.Max);
    }

    [Fact]
    public void SparseWorld_TrimVoxelsOutside_DropsChunksFullyOutside()
    {
        var world = new SparseVoxelWorld(MakeCatalog(), new AirGenerator(), new ChunkSize(4, 4, 4));
        world.EnsureChunk(new ChunkCoord(0, 0, 0)); // voxel box x 0..3
        world.EnsureChunk(new ChunkCoord(5, 0, 0)); // voxel box x 20..23

        world.TrimVoxelsOutside(0, 0, 0, 3, 0, 0);

        Assert.True(world.IsChunkLoaded(new ChunkCoord(0, 0, 0)));
        Assert.False(world.IsChunkLoaded(new ChunkCoord(5, 0, 0)));
    }

    [Fact]
    public void ArrayWorld_EnsureAndTrim_AreNoOps()
    {
        var world = new ArrayVoxelWorld(4, 4, 4, MakeCatalog());

        world.EnsureVoxels(0, 0, 0, 3, 3, 3);
        world.TrimVoxelsOutside(0, 0, 0, 0, 0, 0);

        // Storage is untouched and still readable.
        Assert.True(world.GetVoxel(1, 1, 1).IsAir);
    }

    // ---- Grid version ------------------------------------------------

    [Fact]
    public void Grid_Version_StartsZeroAndBumps()
    {
        var world = new ArrayVoxelWorld(4, 4, 4, MakeCatalog());
        var grid = new VoxelChunkGrid(world, default, 4, 4, 4, Vector3.One);

        Assert.Equal(0, grid.Version);
        grid.BumpVersion();
        Assert.Equal(1, grid.Version);
    }

    // ---- Visual rebuild trigger --------------------------------------

    private static (VoxelChunkGrid grid, VoxelChunkVisual3D visual) MakeVisual()
    {
        var world = new ArrayVoxelWorld(4, 4, 4, MakeCatalog());
        var grid = new VoxelChunkGrid(world, default, 4, 4, 4, Vector3.One);
        return (grid, new VoxelChunkVisual3D(grid));
    }

    [Fact]
    public void Visual_NeedsRebuild_TrueUntilFirstBuild()
    {
        var (_, visual) = MakeVisual();
        Assert.True(visual.NeedsRebuild());

        visual.Rebuild();
        Assert.False(visual.NeedsRebuild());
    }

    [Fact]
    public void Visual_NeedsRebuild_AfterGridVersionBump()
    {
        var (grid, visual) = MakeVisual();
        visual.Rebuild();
        Assert.False(visual.NeedsRebuild());

        grid.BumpVersion();
        Assert.True(visual.NeedsRebuild());
    }

    [Fact]
    public void Visual_Invalidate_ForcesRebuild()
    {
        var (_, visual) = MakeVisual();
        visual.Rebuild();
        Assert.False(visual.NeedsRebuild());

        visual.Invalidate();
        Assert.True(visual.NeedsRebuild());
    }

    // ---- Hit-shape solid-Y band invalidation -------------------------

    [Fact]
    public void HitShape_Band_GatesOnGridVersion()
    {
        var world = new ArrayVoxelWorld(4, 4, 4, MakeCatalog());
        var grid = new VoxelChunkGrid(world, default, 4, 4, 4, Vector3.One);
        var shape = new VoxelHitShape3D(grid);
        var posed = new PosedHitShape3D(shape, Pose3D.Identity);

        // Probe sphere centered on cell (1, 3, 1) — the top layer.
        var probe = new PosedHitShape3D(
            new SphereHitShape3D(Vector3.Zero, 0.4f),
            new Pose3D(new Vector3(1.5f, 3.5f, 1.5f)));

        // Empty world: no hit, and this caches an empty Y band at v0.
        Assert.False(posed.TestHit(probe));

        // Add a solid cell, but don't bump: the cached band is stale and
        // the version gate keeps it, so the new cell is invisible.
        world.SetVoxel(1, 3, 1, world.Catalog["stone"]);
        Assert.False(posed.TestHit(probe));

        // Bump the version (what the source does on a change event); the
        // band rescans and now sees the solid layer.
        grid.BumpVersion();
        Assert.True(posed.TestHit(probe));
    }

    // ---- Source routes voxel ranges to playfield-chunk versions ------

    private static VoxelChunkBarrier3D BarrierOf(VoxelChunkSource3D source, ChunkCoord coord)
    {
        var chunk = source.GetChunk(in coord);
        Assert.NotNull(chunk);
        return (VoxelChunkBarrier3D)chunk!.Barriers[0];
    }

    private static VoxelChunkSource3D MakeSource()
    {
        var world = new SparseVoxelWorld(MakeCatalog(), new AirGenerator(), new ChunkSize(4, 4, 4));
        return new VoxelChunkSource3D(world, Vector3.One, 4, 4, 4);
    }

    [Fact]
    public void Source_Edit_BumpsOwningChunk()
    {
        var source = MakeSource();
        var b0 = BarrierOf(source, new ChunkCoord(0, 0, 0));
        b0.VoxelVisual.Rebuild();
        Assert.False(b0.VoxelVisual.NeedsRebuild());

        source.World.SetVoxel(1, 1, 1, source.World.Catalog["stone"]); // interior of chunk (0,0,0)

        Assert.True(b0.VoxelVisual.NeedsRebuild());
    }

    [Fact]
    public void Source_BoundaryEdit_BumpsFaceNeighbor()
    {
        var source = MakeSource();
        var b0 = BarrierOf(source, new ChunkCoord(0, 0, 0));
        var b1 = BarrierOf(source, new ChunkCoord(1, 0, 0));
        b0.VoxelVisual.Rebuild();
        b1.VoxelVisual.Rebuild();

        // Cell x=3 is chunk 0's +X boundary; chunk 1's mesh reads it for
        // face culling, so both must re-mesh.
        source.World.SetVoxel(3, 1, 1, source.World.Catalog["stone"]);

        Assert.True(b0.VoxelVisual.NeedsRebuild());
        Assert.True(b1.VoxelVisual.NeedsRebuild());
    }

    [Fact]
    public void Source_InteriorEdit_DoesNotBumpNeighbor()
    {
        var source = MakeSource();
        var b0 = BarrierOf(source, new ChunkCoord(0, 0, 0));
        var b1 = BarrierOf(source, new ChunkCoord(1, 0, 0));
        b0.VoxelVisual.Rebuild();
        b1.VoxelVisual.Rebuild();

        // Cell x=1 is interior to chunk 0; chunk 1 never reads it.
        source.World.SetVoxel(1, 1, 1, source.World.Catalog["stone"]);

        Assert.True(b0.VoxelVisual.NeedsRebuild());
        Assert.False(b1.VoxelVisual.NeedsRebuild());
    }

    [Fact]
    public void Source_BoundaryEdit_DoesNotBumpDistantChunk()
    {
        var source = MakeSource();
        var b0 = BarrierOf(source, new ChunkCoord(0, 0, 0));
        var b2 = BarrierOf(source, new ChunkCoord(2, 0, 0)); // voxel x 8..11
        b0.VoxelVisual.Rebuild();
        b2.VoxelVisual.Rebuild();

        source.World.SetVoxel(3, 1, 1, source.World.Catalog["stone"]); // chunk 0's boundary, far from chunk 2

        Assert.True(b0.VoxelVisual.NeedsRebuild());
        Assert.False(b2.VoxelVisual.NeedsRebuild());
    }
}
