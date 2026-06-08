using Blitter.Bits;

namespace Blitter.Tests;

public class CubeVoxelShapeTests
{
    private static Texture2D Tex() => Bitmap.Create(1, 1);

    [Fact]
    public void SingleTexture_MapsToEveryFace()
    {
        var tex = Tex();
        var shape = new CubeVoxelShape(tex);

        foreach (VoxelFace face in Enum.GetValues<VoxelFace>())
            Assert.Same(tex, shape.Texture!.GetFace(face));
    }

    [Fact]
    public void CapsAndSides_PutCapsOnTopAndBottom()
    {
        var caps = Tex();
        var sides = Tex();
        var shape = new CubeVoxelShape(topBottom: caps, sides: sides);

        Assert.Same(caps, shape.Texture!.GetFace(VoxelFace.PositiveY));
        Assert.Same(caps, shape.Texture!.GetFace(VoxelFace.NegativeY));
        Assert.Same(sides, shape.Texture!.GetFace(VoxelFace.PositiveX));
        Assert.Same(sides, shape.Texture!.GetFace(VoxelFace.NegativeX));
        Assert.Same(sides, shape.Texture!.GetFace(VoxelFace.PositiveZ));
        Assert.Same(sides, shape.Texture!.GetFace(VoxelFace.NegativeZ));
    }

    [Fact]
    public void TopSidesBottom_MapEachGroup()
    {
        var top = Tex();
        var sides = Tex();
        var bottom = Tex();
        var shape = new CubeVoxelShape(top: top, sides: sides, bottom: bottom);

        Assert.Same(top, shape.Texture!.GetFace(VoxelFace.PositiveY));
        Assert.Same(bottom, shape.Texture!.GetFace(VoxelFace.NegativeY));
        Assert.Same(sides, shape.Texture!.GetFace(VoxelFace.PositiveX));
        Assert.Same(sides, shape.Texture!.GetFace(VoxelFace.NegativeZ));
    }

    [Fact]
    public void SixFaces_MapInVoxelFaceOrder()
    {
        var nx = Tex();
        var px = Tex();
        var ny = Tex();
        var py = Tex();
        var nz = Tex();
        var pz = Tex();
        var shape = new CubeVoxelShape(nx, px, ny, py, nz, pz);

        Assert.Same(nx, shape.Texture!.GetFace(VoxelFace.NegativeX));
        Assert.Same(px, shape.Texture!.GetFace(VoxelFace.PositiveX));
        Assert.Same(ny, shape.Texture!.GetFace(VoxelFace.NegativeY));
        Assert.Same(py, shape.Texture!.GetFace(VoxelFace.PositiveY));
        Assert.Same(nz, shape.Texture!.GetFace(VoxelFace.NegativeZ));
        Assert.Same(pz, shape.Texture!.GetFace(VoxelFace.PositiveZ));
    }

    [Fact]
    public void Transparency_FlowsThroughConvenienceConstructors()
    {
        var shape = new CubeVoxelShape(topBottom: Tex(), sides: Tex(), transparency: TransparencyMode.Blend);
        Assert.Equal(TransparencyMode.Blend, shape.Transparency);
    }
}
