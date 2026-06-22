using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

public class VoxelMesherTests
{
    /// <summary>Counts quads and triangles routed through the builder.</summary>
    private sealed class CountingMeshBuilder : IChunkMeshBuilder
    {
        public int QuadCount { get; private set; }
        public int TriangleCount { get; private set; }

        public void AddQuad(
            Texture2D? sourceTexture,
            TransparencyMode transparency,
            in LitTextureVertex3D v0,
            in LitTextureVertex3D v1,
            in LitTextureVertex3D v2,
            in LitTextureVertex3D v3) => QuadCount++;

        public void AddTriangle(
            Texture2D? sourceTexture,
            TransparencyMode transparency,
            in LitTextureVertex3D v0,
            in LitTextureVertex3D v1,
            in LitTextureVertex3D v2) => TriangleCount++;
    }

    private static (ArrayVoxelWorld world, VoxelCatalog catalog) MakeWorld(int w, int h, int d)
    {
        var catalog = new VoxelCatalog();
        catalog.Add(new VoxelType { Name = "stone", IsOpaque = true });
        catalog.Add(new VoxelType { Name = "glass", IsOpaque = false });
        return (new ArrayVoxelWorld(w, h, d, catalog), catalog);
    }

    private static int CountQuads(ArrayVoxelWorld world)
    {
        var grid = new VoxelChunkGrid(world, default, world.Width, world.Height, world.Depth, Vector3.One);
        var builder = new CountingMeshBuilder();
        VoxelMesher.Build(grid, builder);
        return builder.QuadCount;
    }

    [Fact]
    public void IsolatedBlock_EmitsSixQuads()
    {
        var (world, catalog) = MakeWorld(1, 1, 1);
        world.SetVoxel(0, 0, 0, catalog["stone"]);
        Assert.Equal(6, CountQuads(world));
    }

    [Fact]
    public void EmptyGrid_EmitsNothing()
    {
        var (world, _) = MakeWorld(2, 2, 2);
        Assert.Equal(0, CountQuads(world));
    }

    [Fact]
    public void AdjacentBlocks_CullSharedFaces()
    {
        var (world, catalog) = MakeWorld(2, 1, 1);
        world.SetVoxel(0, 0, 0, catalog["stone"]);
        world.SetVoxel(1, 0, 0, catalog["stone"]);
        // 6 + 6 minus the two touching faces = 10.
        Assert.Equal(10, CountQuads(world));
    }

    [Fact]
    public void TransparentNeighbor_DoesNotCullFace()
    {
        var (world, catalog) = MakeWorld(2, 1, 1);
        world.SetVoxel(0, 0, 0, catalog["stone"]); // opaque stone
        world.SetVoxel(1, 0, 0, catalog["glass"]); // glass (FullBlock, not opaque)

        // Stone keeps all 6 faces (glass neighbor isn't opaque); glass
        // keeps all 6 faces too (stone neighbor IS opaque, so glass's
        // -X face toward stone is culled). Stone 6 + glass 5 = 11.
        Assert.Equal(11, CountQuads(world));
    }

    [Fact]
    public void SameTransparentType_CullsSharedFace()
    {
        var (world, catalog) = MakeWorld(2, 1, 1);
        world.SetVoxel(0, 0, 0, catalog["glass"]); // glass
        world.SetVoxel(1, 0, 0, catalog["glass"]); // glass

        // Adjacent panes of the same non-opaque voxel cull the doubled
        // interior face between them, even though neither is opaque.
        // 6 + 6 minus the two touching faces = 10.
        Assert.Equal(10, CountQuads(world));
    }

    [Fact]
    public void DifferentTransparentTypes_KeepSharedFace()
    {
        var (world, catalog) = MakeWorld(2, 1, 1);
        catalog.Add(new VoxelType { Name = "ice", IsOpaque = false });
        world.SetVoxel(0, 0, 0, catalog["glass"]); // glass
        world.SetVoxel(1, 0, 0, catalog["ice"]); // ice (different non-opaque type)

        // Two different see-through types don't cull against each other,
        // so both shared faces survive: 6 + 6 = 12.
        Assert.Equal(12, CountQuads(world));
    }
}
