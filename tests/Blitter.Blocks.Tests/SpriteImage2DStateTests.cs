namespace Blitter.Blocks.Tests;

public class SpriteImage2DStateTests
{
    [Fact]
    public void Default_HasSingleDefaultState()
    {
        using var tex = Bitmap.Create(4, 4);
        SpriteImage2D image = new TextureSpriteImage2D(tex);

        Assert.Equal(SpriteImage2D.DefaultState, image.State);
        Assert.Single(image.States);
        Assert.Equal(SpriteImage2D.DefaultState, image.States[0]);
    }

    [Fact]
    public void State_IsSettable()
    {
        using var tex = Bitmap.Create(4, 4);
        SpriteImage2D image = new TextureSpriteImage2D(tex);

        image.State = "walk";
        Assert.Equal("walk", image.State);
    }
}
