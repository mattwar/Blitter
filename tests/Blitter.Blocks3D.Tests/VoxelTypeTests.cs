namespace Blitter.Tests;

public class VoxelTypeTests
{
    [Fact]
    public void Air_IsTheCanonicalEmptyCell()
    {
        Assert.Equal(0, VoxelType.Air.Id);
        Assert.True(VoxelType.Air.IsAir);
        Assert.False(VoxelType.Air.IsOpaque);
        Assert.Equal(VoxelShape.None, VoxelType.Air.Shape);
    }

    [Fact]
    public void Defaults_AreOpaqueFullBlock()
    {
        var type = new VoxelType { Id = 1, Name = "stone" };
        Assert.False(type.IsAir);
        Assert.True(type.IsOpaque);
        Assert.Equal(VoxelShape.FullBlock, type.Shape);
    }

    [Fact]
    public void GetFaceTexture_AllNull_ReturnsNullForEveryFace()
    {
        var type = new VoxelType { Id = 1 };
        for (int face = 0; face < 6; face++)
            Assert.Null(type.GetFaceTexture(face));
    }
}
