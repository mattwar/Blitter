using System.Collections.Immutable;

using Blitter.Bits;

namespace Blitter.Tests;

public class AnimationCatalogFactoriesTests
{
    private static TextureCatalog MakeAtlas(int count)
    {
        var textures = new Texture2D[count];
        for (int i = 0; i < count; i++) textures[i] = new FakeTexture2D();
        return new TextureCatalog(textures);
    }

    [Fact]
    public void ToAnimationCatalog_BuildsNamedSequence()
    {
        var atlas = MakeAtlas(4);
        var spec = new AnimationCatalogFactories.Spec(
            "walk", ImmutableArray.Create(0, 1, 2, 3), TimeSpan.FromMilliseconds(100));

        var catalog = atlas.ToAnimationCatalog([spec]);

        Assert.Equal(1, catalog.Count);
        Assert.True(catalog.Contains("walk"));
        var seq = catalog["walk"];
        Assert.Equal(4, seq.FrameCount);
        Assert.Equal(TimeSpan.FromMilliseconds(100), seq.FrameDuration);
    }

    [Fact]
    public void ToAnimationCatalog_BuildsMultipleSequences()
    {
        var atlas = MakeAtlas(6);
        AnimationCatalogFactories.Spec[] specs =
        [
            new("idle", ImmutableArray.Create(0, 1), TimeSpan.FromMilliseconds(200)),
            new("run", ImmutableArray.Create(2, 3, 4, 5), TimeSpan.FromMilliseconds(80)),
        ];

        var catalog = atlas.ToAnimationCatalog(specs);

        Assert.Equal(2, catalog.Count);
        Assert.Equal(2, catalog["idle"].FrameCount);
        Assert.Equal(4, catalog["run"].FrameCount);
    }

    [Fact]
    public void ToAnimationCatalog_FrameOutOfRange_Throws()
    {
        var atlas = MakeAtlas(2);
        var spec = new AnimationCatalogFactories.Spec(
            "bad", ImmutableArray.Create(0, 5), TimeSpan.FromMilliseconds(100));

        Assert.Throws<ArgumentOutOfRangeException>(() => atlas.ToAnimationCatalog([spec]));
    }

    [Fact]
    public void ToAnimationCatalog_EmptyFrames_Throws()
    {
        var atlas = MakeAtlas(2);
        var spec = new AnimationCatalogFactories.Spec(
            "empty", ImmutableArray<int>.Empty, TimeSpan.FromMilliseconds(100));

        Assert.Throws<ArgumentException>(() => atlas.ToAnimationCatalog([spec]));
    }

    [Fact]
    public void ToAnimationCatalog_NullAtlas_Throws()
    {
        TextureCatalog? atlas = null;
        var spec = new AnimationCatalogFactories.Spec(
            "x", ImmutableArray.Create(0), TimeSpan.FromMilliseconds(100));

        Assert.Throws<ArgumentNullException>(() => atlas!.ToAnimationCatalog([spec]));
    }

    [Fact]
    public void ToSingleSequenceCatalog_NoFrames_UsesEveryAtlasFrame()
    {
        var atlas = MakeAtlas(3);
        var catalog = atlas.ToSingleSequenceCatalog(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, catalog.Count);
        Assert.True(catalog.Contains("default"));
        Assert.Equal(3, catalog["default"].FrameCount);
    }

    [Fact]
    public void ToSingleSequenceCatalog_SubsetFrames_UsesGivenFrames()
    {
        var atlas = MakeAtlas(5);
        var catalog = atlas.ToSingleSequenceCatalog(
            TimeSpan.FromMilliseconds(50),
            frames: [1, 3],
            name: "sub");

        Assert.Equal(2, catalog["sub"].FrameCount);
    }

    [Fact]
    public void ToSingleSequenceCatalog_EmptyAtlas_Throws()
    {
        var atlas = MakeAtlas(0);
        Assert.Throws<ArgumentException>(
            () => atlas.ToSingleSequenceCatalog(TimeSpan.FromMilliseconds(50)));
    }
}
