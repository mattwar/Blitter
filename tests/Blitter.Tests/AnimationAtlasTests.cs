using Blitter.Bits;

namespace Blitter.Tests;

public class AnimationAtlasTests
{
    [Fact]
    public void Default_State_Is_First_Sequence()
    {
        using var atlas = MakeAtlas(8);
        var a = new AnimationAtlas(atlas, [
            new("idle", [0, 1], TimeSpan.FromSeconds(1)),
            new("walk", [2, 3, 4], TimeSpan.FromSeconds(1)),
        ]);

        Assert.Equal("idle", a.DefaultState);
        Assert.Equal(new[] { "idle", "walk" }, a.States);
    }

    [Fact]
    public void Explicit_DefaultState_Is_Honored()
    {
        using var atlas = MakeAtlas(8);
        var a = new AnimationAtlas(atlas, [
            new("idle", [0], TimeSpan.FromSeconds(1)),
            new("walk", [1, 2], TimeSpan.FromSeconds(1)),
        ], defaultState: "walk");

        Assert.Equal("walk", a.DefaultState);
    }

    [Fact]
    public void Unknown_DefaultState_Throws()
    {
        using var atlas = MakeAtlas(4);
        Assert.Throws<ArgumentException>(() =>
            new AnimationAtlas(atlas, [
                new("idle", [0], TimeSpan.FromSeconds(1)),
            ], defaultState: "missing"));
    }

    [Fact]
    public void Out_Of_Range_Frame_Throws()
    {
        using var atlas = MakeAtlas(2);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AnimationAtlas(atlas, [
                new("a", [0, 5], TimeSpan.FromSeconds(1)),
            ]));
    }

    [Fact]
    public void Duplicate_Names_Throw()
    {
        using var atlas = MakeAtlas(4);
        Assert.Throws<ArgumentException>(() =>
            new AnimationAtlas(atlas, [
                new("a", [0], TimeSpan.FromSeconds(1)),
                new("a", [1], TimeSpan.FromSeconds(1)),
            ]));
    }

    [Fact]
    public void Lookup_By_Name_And_Index()
    {
        using var atlas = MakeAtlas(4);
        var walk = new AnimationSequence("walk", [1, 2], TimeSpan.FromSeconds(1));
        var a = new AnimationAtlas(atlas, [
            new("idle", [0], TimeSpan.FromSeconds(1)),
            walk,
        ]);

        Assert.Same(walk, a["walk"]);
        Assert.Same(walk, a[1]);
        Assert.True(a.TryGet("walk", out var got));
        Assert.Same(walk, got);
        Assert.False(a.TryGet("missing", out _));
    }

    [Fact]
    public void Single_Factory_Builds_One_Sequence_Atlas()
    {
        using var atlas = MakeAtlas(3);
        var a = AnimationAtlas.Single(atlas, TimeSpan.FromSeconds(1));

        Assert.Equal("default", a.DefaultState);
        Assert.Equal(3, a["default"].FrameCount);
    }

    private static Atlas MakeAtlas(int frames)
    {
        var bmp = Bitmap.Create(frames * 4, 4);
        return Atlas.Grid(bmp, frames, 1);
    }
}
