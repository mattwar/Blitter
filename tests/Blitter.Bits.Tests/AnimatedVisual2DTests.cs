using Blitter.Bits;

namespace Blitter.Tests;

public class AnimatedVisual2DTests
{
    [Fact]
    public void Default_State_Plays_First_Sequence()
    {
        using var bmp = Bitmap.Create(16, 4);
        var atlas = TextureCatalog.Grid(bmp, 4, 1);
        var aa = atlas.ToAnimationCatalog([
            new("walk", [0, 1, 2, 3], TimeSpan.FromSeconds(1)),
        ]);
        var v = new AnimatedVisual2D(aa);

        Assert.Equal("walk", v.State);
        Assert.Equal(0, v.FrameIndexAt(TimeSpan.FromSeconds(0)));
        Assert.Equal(2, v.FrameIndexAt(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void State_Change_Restarts_Sequence()
    {
        using var bmp = Bitmap.Create(32, 4);
        var atlas = TextureCatalog.Grid(bmp, 8, 1);
        var aa = atlas.ToAnimationCatalog([
            new("idle", [0, 1], TimeSpan.FromSeconds(1)),
            new("attack", [4, 5, 6, 7], TimeSpan.FromSeconds(1)),
        ]);
        var v = new AnimatedVisual2D(aa);

        Assert.Equal(1, v.FrameIndexAt(TimeSpan.FromSeconds(5)));
        v.State = "attack";
        // The first lookup after the switch stamps the local clock base.
        // FrameIndexAt returns the position within the sequence's frame list.
        Assert.Equal(0, v.FrameIndexAt(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, v.FrameIndexAt(TimeSpan.FromSeconds(6)));
        Assert.Equal(3, v.FrameIndexAt(TimeSpan.FromSeconds(8)));
    }

    [Fact]
    public void Unknown_State_Throws()
    {
        using var bmp = Bitmap.Create(8, 4);
        var atlas = TextureCatalog.Grid(bmp, 2, 1);
        var aa = atlas.ToAnimationCatalog([
            new("idle", [0, 1], TimeSpan.FromSeconds(1)),
        ]);
        var v = new AnimatedVisual2D(aa);

        Assert.Throws<ArgumentException>(() => v.State = "missing");
    }

    [Fact]
    public void WithPhaseOffset_Desyncs_Playback()
    {
        using var bmp = Bitmap.Create(16, 4);
        var atlas = TextureCatalog.Grid(bmp, 4, 1);
        var aa = atlas.ToAnimationCatalog([
            new("walk", [0, 1, 2, 3], TimeSpan.FromSeconds(1)),
        ]);
        var a = new AnimatedVisual2D(aa);
        var b = a.WithPhaseOffset(TimeSpan.FromSeconds(2));

        Assert.Equal(0, a.FrameIndexAt(TimeSpan.FromSeconds(0)));
        Assert.Equal(2, b.FrameIndexAt(TimeSpan.FromSeconds(0)));
        Assert.Equal(TimeSpan.Zero, a.PhaseOffset);
        Assert.Equal(TimeSpan.FromSeconds(2), b.PhaseOffset);
        Assert.Same(a.Catalog, b.Catalog);
    }

    [Fact]
    public void IsAtEnd_Tracks_Once_Sequences()
    {
        using var bmp = Bitmap.Create(12, 4);
        var atlas = TextureCatalog.Grid(bmp, 3, 1);
        var aa = atlas.ToAnimationCatalog([
            new("idle", [0, 1], TimeSpan.FromSeconds(1), AnimationLoop.Loop),
            new("attack", [0, 1, 2], TimeSpan.FromSeconds(1), AnimationLoop.Once),
        ]);
        var v = new AnimatedVisual2D(aa);

        Assert.False(v.IsAtEnd(TimeSpan.FromSeconds(50)));
        v.State = "attack";
        Assert.False(v.IsAtEnd(TimeSpan.FromSeconds(50)));      // stamps base = 50
        Assert.False(v.IsAtEnd(TimeSpan.FromSeconds(51)));      // local = 1
        Assert.True(v.IsAtEnd(TimeSpan.FromSeconds(52)));       // local = 2 (last frame)
    }

    [Fact]
    public void Null_Atlas_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AnimatedVisual2D(null!));
    }
}
