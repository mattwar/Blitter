namespace Blitter.Tests;

public class ArrayVoxelWorldTests
{
    private static ArrayVoxelWorld MakeWorld(int w = 4, int h = 4, int d = 4)
    {
        var palette = new VoxelPalette();
        palette.Add(new VoxelType { Id = 1, Name = "stone" });
        return new ArrayVoxelWorld(w, h, d, palette);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 0)]
    public void Constructor_RejectsNonPositiveDimensions(int w, int h, int d)
    {
        var palette = new VoxelPalette();
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArrayVoxelWorld(w, h, d, palette));
    }

    [Fact]
    public void GetVoxel_DefaultsToAir()
    {
        var world = MakeWorld();
        Assert.Equal(0, world.GetVoxel(2, 2, 2));
    }

    [Fact]
    public void SetVoxel_StoresAndReportsChange()
    {
        var world = MakeWorld();
        Assert.True(world.SetVoxel(1, 1, 1, 1));
        Assert.Equal(1, world.GetVoxel(1, 1, 1));
    }

    [Fact]
    public void SetVoxel_SameValue_ReturnsFalse()
    {
        var world = MakeWorld();
        world.SetVoxel(1, 1, 1, 1);
        Assert.False(world.SetVoxel(1, 1, 1, 1));
    }

    [Fact]
    public void OutOfBounds_GetReturnsAir_SetReturnsFalse()
    {
        var world = MakeWorld();
        Assert.Equal(0, world.GetVoxel(-1, 0, 0));
        Assert.Equal(0, world.GetVoxel(100, 0, 0));
        Assert.False(world.SetVoxel(-1, 0, 0, 1));
        Assert.False(world.SetVoxel(100, 0, 0, 1));
    }

    [Fact]
    public void SetVoxel_RaisesSingleCellChange()
    {
        var world = MakeWorld();
        VoxelChangeEventArgs? seen = null;
        world.VoxelsChanged += (_, e) => seen = e;

        world.SetVoxel(2, 3, 1, 1);

        Assert.NotNull(seen);
        Assert.Equal((2, 3, 1, 2, 3, 1),
            (seen!.MinX, seen.MinY, seen.MinZ, seen.MaxX, seen.MaxY, seen.MaxZ));
    }

    [Fact]
    public void Fill_WritesRangeAndCountsCells()
    {
        var world = MakeWorld();
        var written = world.Fill(0, 0, 0, 1, 1, 1, 1);

        Assert.Equal(8, written); // 2*2*2
        Assert.Equal(1, world.GetVoxel(0, 0, 0));
        Assert.Equal(1, world.GetVoxel(1, 1, 1));
        Assert.Equal(0, world.GetVoxel(2, 2, 2));
    }

    [Fact]
    public void Fill_ClipsToBounds()
    {
        var world = MakeWorld(4, 4, 4);
        var written = world.Fill(-5, -5, -5, 100, 100, 100, 1);
        Assert.Equal(64, written); // entire 4*4*4 world
    }

    [Fact]
    public void Fill_RaisesSingleBoundingBoxEvent()
    {
        var world = MakeWorld();
        VoxelChangeEventArgs? seen = null;
        var count = 0;
        world.VoxelsChanged += (_, e) => { seen = e; count++; };

        world.Fill(0, 0, 0, 2, 2, 2, 1);

        Assert.Equal(1, count);
        Assert.Equal((0, 0, 0, 2, 2, 2),
            (seen!.MinX, seen.MinY, seen.MinZ, seen.MaxX, seen.MaxY, seen.MaxZ));
    }

    [Fact]
    public void Fill_NoChange_ReturnsZeroAndRaisesNothing()
    {
        var world = MakeWorld();
        var raised = false;
        world.VoxelsChanged += (_, _) => raised = true;

        var written = world.Fill(0, 0, 0, 1, 1, 1, 0); // already all air

        Assert.Equal(0, written);
        Assert.False(raised);
    }
}
