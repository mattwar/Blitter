using Blitter.Bits;

namespace Blitter.Tests;

public class AnimationCatalogTests
{
    [Fact]
    public void Names_In_Declaration_Order()
    {
        using var atlas = MakeAtlas(8);
        var a = atlas.ToAnimationCatalog([
            new("idle", [0, 1], TimeSpan.FromSeconds(1)),
            new("walk", [2, 3, 4], TimeSpan.FromSeconds(1)),
        ]);

        Assert.Equal(new[] { "idle", "walk" }, a.Names);
        Assert.Equal(2, a.Count);
    }

    [Fact]
    public void Out_Of_Range_Frame_Throws()
    {
        using var atlas = MakeAtlas(2);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            atlas.ToAnimationCatalog([
                new("a", [0, 5], TimeSpan.FromSeconds(1)),
            ]));
    }

    [Fact]
    public void Duplicate_Names_Throw()
    {
        using var atlas = MakeAtlas(4);
        Assert.Throws<ArgumentException>(() =>
            atlas.ToAnimationCatalog([
                new("a", [0], TimeSpan.FromSeconds(1)),
                new("a", [1], TimeSpan.FromSeconds(1)),
            ]));
    }

    [Fact]
    public void Lookup_By_Name_And_Index()
    {
        using var atlas = MakeAtlas(4);
        var a = atlas.ToAnimationCatalog([
            new("idle", [0], TimeSpan.FromSeconds(1)),
            new("walk", [1, 2], TimeSpan.FromSeconds(1)),
        ]);

        var walk = a["walk"];
        Assert.Equal(2, walk.FrameCount);
        Assert.Same(walk, a[1]);
        Assert.True(a.Contains("walk"));
        Assert.False(a.Contains("missing"));
        Assert.True(a.TryGet("walk", out var got));
        Assert.Same(walk, got);
        Assert.False(a.TryGet("missing", out _));
    }

    [Fact]
    public void Single_Factory_Builds_One_Sequence_Catalog()
    {
        using var atlas = MakeAtlas(3);
        var a = atlas.ToSingleSequenceCatalog(TimeSpan.FromSeconds(1));

        Assert.Equal(1, a.Count);
        Assert.Equal(3, a["default"].FrameCount);
    }

    [Fact]
    public void Empty_Sequences_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new AnimationCatalog(Array.Empty<KeyValuePair<string, AnimationSequence>>()));
    }

    private static TextureCatalog MakeAtlas(int frames)
    {
        var bmp = Bitmap.Create(frames * 4, 4);
        return TextureCatalog.Grid(bmp, frames, 1);
    }
}
