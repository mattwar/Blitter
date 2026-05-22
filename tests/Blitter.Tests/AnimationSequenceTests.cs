using System.Collections.Immutable;
using Blitter.Bits;

namespace Blitter.Tests;

public class AnimationSequenceTests
{
    [Fact]
    public void Loop_Wraps_Through_Frames()
    {
        var s = new AnimationSequence("a", [0, 1, 2, 3], TimeSpan.FromSeconds(1));

        Assert.Equal(0, s.FrameIndexAt(TimeSpan.FromSeconds(0)));
        Assert.Equal(1, s.FrameIndexAt(TimeSpan.FromSeconds(1)));
        Assert.Equal(3, s.FrameIndexAt(TimeSpan.FromSeconds(3)));
        Assert.Equal(0, s.FrameIndexAt(TimeSpan.FromSeconds(4)));
        Assert.Equal(2, s.FrameIndexAt(TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void Once_Holds_On_Last_Frame()
    {
        var s = new AnimationSequence("a", [0, 1, 2], TimeSpan.FromSeconds(1), AnimationLoop.Once);

        Assert.Equal(0, s.FrameIndexAt(TimeSpan.FromSeconds(0)));
        Assert.Equal(2, s.FrameIndexAt(TimeSpan.FromSeconds(2)));
        Assert.Equal(2, s.FrameIndexAt(TimeSpan.FromSeconds(50)));
    }

    [Fact]
    public void PingPong_Bounces_Between_Ends()
    {
        var s = new AnimationSequence("a", [0, 1, 2, 3], TimeSpan.FromSeconds(1), AnimationLoop.PingPong);

        var expected = new[] { 0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 2, 1 };
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], s.FrameIndexAt(TimeSpan.FromSeconds(i)));
    }

    [Fact]
    public void Custom_Order_Is_Honored()
    {
        var s = new AnimationSequence("a", [5, 3, 1], TimeSpan.FromSeconds(1));

        Assert.Equal(3, s.FrameCount);
        Assert.Equal(5, s.FrameIndexAt(TimeSpan.FromSeconds(0)));
        Assert.Equal(3, s.FrameIndexAt(TimeSpan.FromSeconds(1)));
        Assert.Equal(1, s.FrameIndexAt(TimeSpan.FromSeconds(2)));
        Assert.Equal(5, s.FrameIndexAt(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void IsAtEnd_Only_True_For_Once_At_Last_Frame()
    {
        var loop = new AnimationSequence("a", [0, 1, 2], TimeSpan.FromSeconds(1));
        var once = new AnimationSequence("b", [0, 1, 2], TimeSpan.FromSeconds(1), AnimationLoop.Once);

        Assert.False(loop.IsAtEnd(TimeSpan.FromSeconds(50)));
        Assert.False(once.IsAtEnd(TimeSpan.FromSeconds(0)));
        Assert.False(once.IsAtEnd(TimeSpan.FromSeconds(1)));
        Assert.True(once.IsAtEnd(TimeSpan.FromSeconds(2)));
        Assert.True(once.IsAtEnd(TimeSpan.FromSeconds(50)));
    }

    [Fact]
    public void Empty_Frames_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new AnimationSequence("a", ImmutableArray<int>.Empty, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentException>(() =>
            new AnimationSequence("a", default, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Invalid_FrameDuration_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AnimationSequence("a", [0], TimeSpan.Zero));
    }

    [Fact]
    public void Empty_Name_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new AnimationSequence("", [0], TimeSpan.FromSeconds(1)));
    }
}
