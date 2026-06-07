using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

public class VoxelHitShape3DTests
{
    private static VoxelChunkGrid MakeGrid(ArrayVoxelWorld world, Vector3 cellSize) =>
        new(world, default, world.Width, world.Height, world.Depth, cellSize);

    private static ArrayVoxelWorld MakeWorld(int w, int h, int d)
    {
        var palette = new VoxelPalette();
        palette.Add(new VoxelType { Id = 1, Name = "stone" });
        return new ArrayVoxelWorld(w, h, d, palette);
    }

    [Fact]
    public void PrimitiveCount_IsTotalCellCount()
    {
        var world = MakeWorld(2, 3, 4);
        var shape = new VoxelHitShape3D(MakeGrid(world, Vector3.One));
        Assert.Equal(24, shape.PrimitiveCount);
    }

    [Fact]
    public void LocalBoundary_EnclosesTheChunk()
    {
        var world = MakeWorld(4, 4, 4);
        var shape = new VoxelHitShape3D(MakeGrid(world, Vector3.One));

        var size = new Vector3(4f, 4f, 4f);
        Assert.Equal(size * 0.5f, shape.LocalBoundary.Center);
        Assert.Equal(size.Length() * 0.5f, shape.LocalBoundary.Radius, 4);
    }

    [Fact]
    public void Visit_EmitsOneBoxPerSolidCell()
    {
        var world = MakeWorld(4, 4, 4);
        world.SetVoxel(0, 0, 0, 1);
        world.SetVoxel(1, 0, 0, 1);
        world.SetVoxel(3, 3, 3, 1);
        var shape = new VoxelHitShape3D(MakeGrid(world, Vector3.One));

        var count = 0;
        shape.Visit(Pose3D.Identity, (in HitPrimitive3D _) => count++);

        Assert.Equal(3, count);
    }

    [Fact]
    public void Visit_EmptyChunk_EmitsNothing()
    {
        var world = MakeWorld(2, 2, 2);
        var shape = new VoxelHitShape3D(MakeGrid(world, Vector3.One));

        var count = 0;
        shape.Visit(Pose3D.Identity, (in HitPrimitive3D _) => count++);

        Assert.Equal(0, count);
    }
}
