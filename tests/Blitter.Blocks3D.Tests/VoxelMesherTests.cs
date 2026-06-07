using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

public class VoxelMesherTests
{
    /// <summary>Counts quads and remembers which textures were routed.</summary>
    private sealed class CountingSink : IVoxelMeshSink
    {
        public int QuadCount { get; private set; }

        public void EmitQuad(
            Texture2D? sourceTexture,
            in LitTextureVertex3D v0,
            in LitTextureVertex3D v1,
            in LitTextureVertex3D v2,
            in LitTextureVertex3D v3) => QuadCount++;
    }

    private static (ArrayVoxelWorld world, VoxelPalette palette) MakeWorld(int w, int h, int d)
    {
        var palette = new VoxelPalette();
        palette.Add(new VoxelType { Id = 1, Name = "stone", IsOpaque = true });
        palette.Add(new VoxelType { Id = 2, Name = "glass", IsOpaque = false });
        return (new ArrayVoxelWorld(w, h, d, palette), palette);
    }

    private static int CountQuads(ArrayVoxelWorld world)
    {
        var grid = new VoxelChunkGrid(world, default, world.Width, world.Height, world.Depth, Vector3.One);
        var sink = new CountingSink();
        VoxelMesher.Build(grid, sink);
        return sink.QuadCount;
    }

    [Fact]
    public void IsolatedBlock_EmitsSixQuads()
    {
        var (world, _) = MakeWorld(1, 1, 1);
        world.SetVoxel(0, 0, 0, 1);
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
        var (world, _) = MakeWorld(2, 1, 1);
        world.SetVoxel(0, 0, 0, 1);
        world.SetVoxel(1, 0, 0, 1);
        // 6 + 6 minus the two touching faces = 10.
        Assert.Equal(10, CountQuads(world));
    }

    [Fact]
    public void TransparentNeighbor_DoesNotCullFace()
    {
        var (world, _) = MakeWorld(2, 1, 1);
        world.SetVoxel(0, 0, 0, 1); // opaque stone
        world.SetVoxel(1, 0, 0, 2); // glass (FullBlock, not opaque)

        // Stone keeps all 6 faces (glass neighbor isn't opaque); glass
        // keeps all 6 faces too (stone neighbor IS opaque, so glass's
        // -X face toward stone is culled). Stone 6 + glass 5 = 11.
        Assert.Equal(11, CountQuads(world));
    }
}
