using Blitter.Bits;

namespace Blitter.Tests;

public class Visual2DStateTests
{
    [Fact]
    public void Default_HasSingleDefaultState()
    {
        using var tex = Bitmap.Create(4, 4);
        Visual2D visual = new TextureVisual2D(tex);

        Assert.Equal(Visual2D.DefaultState, visual.State);
        Assert.Single(visual.States);
        Assert.Equal(Visual2D.DefaultState, visual.States[0]);
    }

    [Fact]
    public void State_IsSettable()
    {
        using var tex = Bitmap.Create(4, 4);
        Visual2D visual = new TextureVisual2D(tex);

        visual.State = "walk";
        Assert.Equal("walk", visual.State);
    }
}
