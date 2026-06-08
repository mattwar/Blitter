namespace Blitter.Tests;

public class VoxelTypeTests
{
    [Fact]
    public void Air_IsTheCanonicalEmptyCell()
    {
        Assert.Equal(0, VoxelType.Air.Id);
        Assert.True(VoxelType.Air.IsAir);
        Assert.False(VoxelType.Air.IsOpaque);
        Assert.Same(EmptyVoxelShape.Instance, VoxelType.Air.Shape);
        Assert.False(VoxelType.Air.Shape.FillsVoxel);
    }

    [Fact]
    public void Defaults_AreOpaqueFullCube()
    {
        var type = new VoxelType { Id = 1, Name = "stone" };
        Assert.False(type.IsAir);
        Assert.True(type.IsOpaque);
        Assert.IsType<CubeVoxelShape>(type.Shape);
        Assert.True(type.Shape.FillsVoxel);
    }

    [Fact]
    public void DefaultCube_HasNoTexture()
    {
        var type = new VoxelType { Id = 1 };
        var cube = Assert.IsType<CubeVoxelShape>(type.Shape);
        Assert.Null(cube.Texture);
    }
}
