using Blitter.Bits;

namespace Blitter.Tests;

public class AnimatedVisual2DTests
{
    [Fact]
    public void Loop_Wraps_Through_Frames()
    {
        using var atlas = MakeAtlas(4);
        var v = new AnimatedVisual2D(atlas, TimeSpan.FromSeconds(1));

        Assert.Equal(0, v.FrameIndexAt(TimeSpan.FromSeconds(0)));
        Assert.Equal(1, v.FrameIndexAt(TimeSpan.FromSeconds(1)));
        Assert.Equal(3, v.FrameIndexAt(TimeSpan.FromSeconds(3)));
        Assert.Equal(0, v.FrameIndexAt(TimeSpan.FromSeconds(4)));
        Assert.Equal(2, v.FrameIndexAt(TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void Once_Holds_On_Last_Frame()
    {
        using var atlas = MakeAtlas(3);
        var v = new AnimatedVisual2D(atlas, TimeSpan.FromSeconds(1), AnimationLoop.Once);

        Assert.Equal(0, v.FrameIndexAt(TimeSpan.FromSeconds(0)));
        Assert.Equal(2, v.FrameIndexAt(TimeSpan.FromSeconds(2)));
        Assert.Equal(2, v.FrameIndexAt(TimeSpan.FromSeconds(50)));
    }

    [Fact]
    public void PingPong_Bounces_Between_Ends()
    {
        using var atlas = MakeAtlas(4);
        var v = new AnimatedVisual2D(atlas, TimeSpan.FromSeconds(1), AnimationLoop.PingPong);

        // Period for n=4 is 6: 0,1,2,3,2,1, 0,1,2,3,2,1, ...
        var expected = new[] { 0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 2, 1 };
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], v.FrameIndexAt(TimeSpan.FromSeconds(i)));
    }

    [Fact]
    public void Custom_Frames_Are_Used_In_Order()
    {
        using var atlas = MakeAtlas(6);
        var v = new AnimatedVisual2D(
            atlas,
            TimeSpan.FromSeconds(1),
            frames: new[] { 5, 3, 1 });

        Assert.Equal(3, v.FrameCount);
        Assert.Equal(5, v.FrameIndexAt(TimeSpan.FromSeconds(0)));
        Assert.Equal(3, v.FrameIndexAt(TimeSpan.FromSeconds(1)));
        Assert.Equal(1, v.FrameIndexAt(TimeSpan.FromSeconds(2)));
        Assert.Equal(5, v.FrameIndexAt(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void WithOffset_Desyncs_Playback()
    {
        using var atlas = MakeAtlas(4);
        var a = new AnimatedVisual2D(atlas, TimeSpan.FromSeconds(1));
        var b = a.WithOffset(TimeSpan.FromSeconds(2));

        Assert.Equal(0, a.FrameIndexAt(TimeSpan.FromSeconds(0)));
        Assert.Equal(2, b.FrameIndexAt(TimeSpan.FromSeconds(0)));
        // Original is unchanged.
        Assert.Equal(TimeSpan.Zero, a.Offset);
        Assert.Equal(TimeSpan.FromSeconds(2), b.Offset);
        // Same atlas + same frames + same duration.
        Assert.Same(a.Atlas, b.Atlas);
        Assert.Equal(a.FrameDuration, b.FrameDuration);
    }

    [Fact]
    public void WithFrameDuration_Returns_Independent_Copy()
    {
        using var atlas = MakeAtlas(4);
        var a = new AnimatedVisual2D(atlas, TimeSpan.FromSeconds(1));
        var b = a.WithFrameDuration(TimeSpan.FromSeconds(0.5));

        Assert.Equal(2, b.FrameIndexAt(TimeSpan.FromSeconds(1)));
        Assert.Equal(1, a.FrameIndexAt(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Invalid_FrameDuration_Throws()
    {
        using var atlas = MakeAtlas(2);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AnimatedVisual2D(atlas, TimeSpan.Zero));
    }

    [Fact]
    public void Out_Of_Range_Frame_Throws()
    {
        using var atlas = MakeAtlas(2);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AnimatedVisual2D(atlas, TimeSpan.FromSeconds(1), frames: new[] { 0, 2 }));
    }

    private static Atlas MakeAtlas(int frames)
    {
        var bmp = Bitmap.Create(frames * 4, 4);
        return Atlas.Grid(bmp, frames, 1);
    }
}
