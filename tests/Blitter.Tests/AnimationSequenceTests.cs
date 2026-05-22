using System.Collections.Immutable;
using Blitter;
using Blitter.Bits;

namespace Blitter.Tests;

public class AnimationSequenceTests
{
    [Fact]
    public void Loop_Wraps_Through_Frames()
    {
        var frames = MakeFrames(4);
        var s = new AnimationSequence(frames, TimeSpan.FromSeconds(1));

        Assert.Equal(0, s.FrameIndexAt(TimeSpan.FromSeconds(0)));
        Assert.Equal(1, s.FrameIndexAt(TimeSpan.FromSeconds(1)));
        Assert.Equal(3, s.FrameIndexAt(TimeSpan.FromSeconds(3)));
        Assert.Equal(0, s.FrameIndexAt(TimeSpan.FromSeconds(4)));
        Assert.Equal(2, s.FrameIndexAt(TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void Once_Holds_On_Last_Frame()
    {
        var s = new AnimationSequence(MakeFrames(3), TimeSpan.FromSeconds(1), AnimationLoop.Once);

        Assert.Equal(0, s.FrameIndexAt(TimeSpan.FromSeconds(0)));
        Assert.Equal(2, s.FrameIndexAt(TimeSpan.FromSeconds(2)));
        Assert.Equal(2, s.FrameIndexAt(TimeSpan.FromSeconds(50)));
    }

    [Fact]
    public void PingPong_Bounces_Between_Ends()
    {
        var s = new AnimationSequence(MakeFrames(4), TimeSpan.FromSeconds(1), AnimationLoop.PingPong);

        var expected = new[] { 0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 2, 1 };
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], s.FrameIndexAt(TimeSpan.FromSeconds(i)));
    }

    [Fact]
    public void FrameAt_Returns_Requested_Texture()
    {
        var frames = MakeFrames(3);
        var s = new AnimationSequence(frames, TimeSpan.FromSeconds(1));

        Assert.Equal(3, s.FrameCount);
        Assert.Same(frames[0], s.FrameAt(TimeSpan.FromSeconds(0)));
        Assert.Same(frames[1], s.FrameAt(TimeSpan.FromSeconds(1)));
        Assert.Same(frames[2], s.FrameAt(TimeSpan.FromSeconds(2)));
        Assert.Same(frames[0], s.FrameAt(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void IsAtEnd_Only_True_For_Once_At_Last_Frame()
    {
        var loop = new AnimationSequence(MakeFrames(3), TimeSpan.FromSeconds(1));
        var once = new AnimationSequence(MakeFrames(3), TimeSpan.FromSeconds(1), AnimationLoop.Once);

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
            new AnimationSequence(ImmutableArray<Texture2D>.Empty, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentException>(() =>
            new AnimationSequence(default, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Null_Frame_Throws()
    {
        var frames = ImmutableArray.Create<Texture2D>(Bitmap.Create(1, 1), null!);
        Assert.Throws<ArgumentException>(() =>
            new AnimationSequence(frames, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Invalid_FrameDuration_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AnimationSequence(MakeFrames(1), TimeSpan.Zero));
    }

    private static ImmutableArray<Texture2D> MakeFrames(int count)
    {
        var b = ImmutableArray.CreateBuilder<Texture2D>(count);
        for (int i = 0; i < count; i++) b.Add(Bitmap.Create(2, 2));
        return b.MoveToImmutable();
    }
}
