using System.Numerics;
using Blitter.Bits;

namespace Blitter.Tests;

public class ImageSourceTests : IDisposable
{
    private readonly string _dir;

    public ImageSourceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "blitter-imagesource-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        ImageSource.Clear();
    }

    public void Dispose()
    {
        ImageSource.Clear();
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string SaveImage(string name = "img.png", int w = 32, int h = 32)
    {
        var path = Path.Combine(_dir, name);
        using var bmp = Bitmap.Create(w, h, PixelFormat.RGBA8888);
        bmp.Save(path);
        return path;
    }

    [Fact]
    public void ToTexture_WholeImage_ReturnsBitmap()
    {
        var path = SaveImage(w: 24, h: 24);
        var texture = new ImageSource { FilePath = path }.ToTexture();

        Assert.IsType<Bitmap>(texture);
        Assert.Equal((24, 24), texture.Size);
    }

    [Fact]
    public void ToTexture_Tile_SlicesRegion()
    {
        var path = SaveImage(w: 64, h: 48);
        var texture = new ImageSource
        {
            FilePath = path,
            Tile = (2, 1),
            TileSize = (16, 16),
        }.ToTexture();

        var region = Assert.IsAssignableFrom<ITextureRegion>(texture);
        Assert.Equal(new Rect(32, 16, 16, 16), region.Region);
    }

    [Fact]
    public void ToTexture_TileWithoutTileSize_Throws()
    {
        var path = SaveImage();
        var source = new ImageSource { FilePath = path, Tile = (0, 0) };
        Assert.Throws<InvalidOperationException>(() => source.ToTexture());
    }

    [Fact]
    public void ToTexture_NoFilePath_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ImageSource().ToTexture());
    }

    [Fact]
    public void Cache_SharesSourceBitmap()
    {
        var path = SaveImage();
        var a = new ImageSource { FilePath = path }.ToTexture();
        var b = new ImageSource { FilePath = path }.ToTexture();
        Assert.Same(a, b);
    }

    [Fact]
    public void Cache_SharesBitmapAcrossTiles()
    {
        var path = SaveImage(w: 32, h: 32);
        var a = (ITextureRegion)new ImageSource { FilePath = path, Tile = (0, 0), TileSize = (16, 16) }.ToTexture();
        var b = (ITextureRegion)new ImageSource { FilePath = path, Tile = (1, 1), TileSize = (16, 16) }.ToTexture();
        Assert.Same(a.Source, b.Source);
    }

    [Fact]
    public void Evict_ForcesReload()
    {
        var path = SaveImage();
        var first = new ImageSource { FilePath = path }.ToTexture();
        ImageSource.Evict(path);
        var second = new ImageSource { FilePath = path }.ToTexture();
        Assert.NotSame(first, second);
    }

    [Fact]
    public void ToVisual_AutoHint_DerivesFromTexture()
    {
        var path = SaveImage();
        var visual = new ImageSource { FilePath = path }.ToVisual2D();
        Assert.IsType<TextureVisual2D>(visual);
    }

    [Fact]
    public void ToVisual_BoxHint_UsesBoxHitShape()
    {
        var path = SaveImage(w: 20, h: 10);
        var visual = new ImageSource { FilePath = path, Hit = HitShapeHint.Box }.ToVisual2D();
        var box = Assert.IsType<BoxHitShape2D>(visual.HitShape);
        Assert.Equal(new Vector2(10f, 5f), box.LocalHalfExtents);
    }

    [Fact]
    public void ToVisual_CircleHint_UsesCircleHitShape()
    {
        var path = SaveImage(w: 20, h: 10);
        var visual = new ImageSource { FilePath = path, Hit = HitShapeHint.Circle }.ToVisual2D();
        var circle = Assert.IsType<CircleHitShape2D>(visual.HitShape);
        Assert.Equal(5f, circle.LocalRadius);
    }

    [Fact]
    public void ToVisual_NoneHint_UsesNoneHitShape()
    {
        var path = SaveImage();
        var visual = new ImageSource { FilePath = path, Hit = HitShapeHint.None }.ToVisual2D();
        Assert.Same(HitShape2D.None, visual.HitShape);
    }

    [Fact]
    public void ImplicitFromString_SetsFilePath()
    {
        ImageSource source = "hero.png";
        Assert.Equal("hero.png", source.FilePath);
        Assert.Null(source.Tile);
    }

    [Fact]
    public void ImplicitToVisual_Materialises()
    {
        var path = SaveImage();
        Visual2D visual = new ImageSource { FilePath = path };
        Assert.IsType<TextureVisual2D>(visual);
    }

    [Fact]
    public void ResolvesAgainstAssetFolder()
    {
        SaveImage("root.png", 8, 8);
        var previous = Application.Current.AssetFolder;
        try
        {
            Application.Current.AssetFolder = _dir;
            var texture = new ImageSource { FilePath = "root.png" }.ToTexture();
            Assert.Equal((8, 8), texture.Size);
        }
        finally
        {
            Application.Current.AssetFolder = previous;
        }
    }

    [Fact]
    public void States_DistinctFiles_ProduceAnimatedVisualWithNamedStates()
    {
        var idle = SaveImage("idle.png", 16, 16);
        var thrust = SaveImage("thrust.png", 16, 16);

        var source = new ImageSource
        {
            ["idle"] = idle,
            ["thrust"] = thrust,
        };

        Assert.True(source.HasStates);
        var visual = Assert.IsType<AnimatedVisual2D>(source.ToVisual2D());
        Assert.Equal(["idle", "thrust"], visual.States);
        // First declared state is the initial one.
        Assert.Equal("idle", visual.State);
    }

    [Fact]
    public void States_ChildrenInheritSheetAndTileSize()
    {
        var sheet = SaveImage("sheet.png", 64, 16);

        var source = new ImageSource
        {
            FilePath = sheet,
            TileSize = (16, 16),
            ["a"] = { Tile = (0, 0) },
            ["b"] = { Tile = (3, 0) },
        };

        var visual = Assert.IsType<AnimatedVisual2D>(source.ToVisual2D());
        var a = (ITextureRegion)visual.Catalog["a"].Frames[0].Texture;
        var b = (ITextureRegion)visual.Catalog["b"].Frames[0].Texture;
        Assert.Equal(new Rect(0, 0, 16, 16), a.Region);
        Assert.Equal(new Rect(48, 0, 16, 16), b.Region);
    }

    [Fact]
    public void States_MultiFrameAnimation_InheritsSheetAndBuildsSequence()
    {
        var sheet = SaveImage("walk.png", 64, 16);

        var source = new ImageSource
        {
            FilePath = sheet,
            TileSize = (16, 16),
            ["walk"] = { Frames = { (1, 0), (2, 0), (3, 0) }, FrameDuration = TimeSpan.FromSeconds(0.2) },
        };

        var visual = Assert.IsType<AnimatedVisual2D>(source.ToVisual2D());
        var walk = visual.Catalog["walk"];
        Assert.Equal(3, walk.Frames.Length);
        Assert.Equal(TimeSpan.FromSeconds(0.2), walk.FrameDuration);
        Assert.Equal(new Rect(16, 0, 16, 16), ((ITextureRegion)walk.Frames[0].Texture).Region);
        Assert.Equal(new Rect(48, 0, 16, 16), ((ITextureRegion)walk.Frames[2].Texture).Region);
    }

    [Fact]
    public void States_Duration_DividesEvenlyAcrossFrames()
    {
        var sheet = SaveImage("jump.png", 64, 16);

        var source = new ImageSource
        {
            FilePath = sheet,
            TileSize = (16, 16),
            ["jump"] = { Frames = { (0, 0), (1, 0), (2, 0), (3, 0) }, Duration = TimeSpan.FromSeconds(1.0) },
        };

        var visual = Assert.IsType<AnimatedVisual2D>(source.ToVisual2D());
        var jump = visual.Catalog["jump"];
        Assert.Equal(TimeSpan.FromSeconds(0.25), jump.FrameDuration);
    }

    [Fact]
    public void States_DurationAndFrameDuration_Throws()
    {
        var sheet = SaveImage("both.png", 64, 16);

        var source = new ImageSource
        {
            FilePath = sheet,
            TileSize = (16, 16),
            ["x"] =
            {
                Frames = { (0, 0), (1, 0) },
                Duration = TimeSpan.FromSeconds(1.0),
                FrameDuration = TimeSpan.FromSeconds(0.2),
            },
        };

        Assert.Throws<InvalidOperationException>(() => source.ToVisual2D());
    }

    [Fact]
    public void States_StateFlip_ComposesOntoEveryFrame()
    {
        var sheet = SaveImage("flipwalk.png", 64, 16);

        var source = new ImageSource
        {
            FilePath = sheet,
            TileSize = (16, 16),
            ["walk-left"] = { Frames = { (0, 0), (1, 0) }, Flip = FlipMode.Horizontal },
        };

        var visual = Assert.IsType<AnimatedVisual2D>(source.ToVisual2D());
        var walk = visual.Catalog["walk-left"];
        Assert.Equal(FlipMode.Horizontal, walk.Frames[0].Flip);
        Assert.Equal(FlipMode.Horizontal, walk.Frames[1].Flip);
    }

    [Fact]
    public void States_TileAndFramesTogether_Throws()
    {
        var sheet = SaveImage("both.png", 32, 16);

        var source = new ImageSource
        {
            FilePath = sheet,
            TileSize = (16, 16),
            ["bad"] = { Tile = (0, 0), Frames = { (1, 0) } },
        };

        Assert.Throws<InvalidOperationException>(() => source.ToVisual2D());
    }

    [Fact]
    public void States_BraceInitializer_VivifiesChild()
    {
        var sheet = SaveImage("sheet.png", 32, 16);

        var source = new ImageSource
        {
            FilePath = sheet,
            TileSize = (16, 16),
            ["only"] = { Tile = (1, 0) },
        };

        Assert.True(source.HasStates);
        var visual = Assert.IsType<AnimatedVisual2D>(source.ToVisual2D());
        Assert.Equal(["only"], visual.States);
    }

    [Fact]
    public void Indexer_RepeatedAccess_ReturnsSameChild()
    {
        var source = new ImageSource();
        var first = source["x"];
        var second = source["x"];
        Assert.Same(first, second);
    }

    [Fact]
    public void Index_FixedGrid_SelectsLinearCellRowMajor()
    {
        // 64x32 sheet of 16px cells => 4 columns, 2 rows.
        var sheet = SaveImage("grid.png", 64, 32);

        var source = new ImageSource { FilePath = sheet, TileSize = (16, 16), Index = 5 };

        // Index 5 wraps to column 1, row 1.
        var region = (ITextureRegion)source.ToTexture();
        Assert.Equal(new Rect(16, 16, 16, 16), region.Region);
    }

    [Fact]
    public void Index_Sensed_SelectsDetectedRegion()
    {
        // Two opaque blocks separated by a transparent column gap; no TileSize
        // means Index picks an auto-sensed region.
        var sheet = SavePainted("sensed.png", 40, 16,
            (2, 0, 8, 16),    // block A
            (20, 0, 10, 16)); // block B

        var source = new ImageSource { FilePath = sheet, Index = 1 };

        var region = (ITextureRegion)source.ToTexture();
        Assert.Equal(new Rect(20, 0, 10, 16), region.Region);
    }

    [Fact]
    public void ExplicitTexture_LeafSource_UsesTextureDirectly()
    {
        var path = SaveImage("explicit.png", 20, 20);
        var tex = new ImageSource { FilePath = path }.ToTexture();

        var source = new ImageSource { Texture = tex };

        Assert.Same(tex, source.ToTexture());
        var visual = Assert.IsType<TextureVisual2D>(source.Visual);
        Assert.Same(tex, visual.Texture);
    }

    [Fact]
    public void ExplicitTexture_ImplicitFromTexture2D()
    {
        var path = SaveImage("implicit-tex.png", 12, 12);
        var tex = new ImageSource { FilePath = path }.ToTexture();

        ImageSource source = tex;

        Assert.Same(tex, source.ToTexture());
    }

    [Fact]
    public void ExplicitTexture_WithFilePath_Throws()
    {
        var path = SaveImage("both-tex.png", 12, 12);
        var tex = new ImageSource { FilePath = path }.ToTexture();

        var source = new ImageSource { Texture = tex, FilePath = path };

        Assert.Throws<InvalidOperationException>(() => source.ToTexture());
    }

    [Fact]
    public void ExplicitFrames_FromTextures_BuildAnimatedVisual()
    {
        var path = SaveImage("frametex.png", 32, 16);
        var whole = new ImageSource { FilePath = path }.ToTexture();
        var a = whole.Slice(new Rect(0, 0, 16, 16));
        var b = whole.Slice(new Rect(16, 0, 16, 16));

        var source = new ImageSource
        {
            ["spin"] = { Frames = { a, b }, FrameDuration = TimeSpan.FromSeconds(0.1) },
        };

        var visual = Assert.IsType<AnimatedVisual2D>(source.ToVisual2D());
        var spin = visual.Catalog["spin"];
        Assert.Equal(2, spin.Frames.Length);
        Assert.Same(a, spin.Frames[0].Texture);
        Assert.Same(b, spin.Frames[1].Texture);
    }

    [Fact]
    public void ExplicitSequence_StateUsesItAsIs()
    {
        var path = SaveImage("seqtex.png", 32, 16);
        var whole = new ImageSource { FilePath = path }.ToTexture();
        var seq = new AnimationSequence(
            [whole.Slice(new Rect(0, 0, 16, 16)), whole.Slice(new Rect(16, 0, 16, 16))],
            TimeSpan.FromSeconds(0.2));

        var source = new ImageSource { ["walk"] = seq };

        var visual = Assert.IsType<AnimatedVisual2D>(source.ToVisual2D());
        Assert.Same(seq, visual.Catalog["walk"]);
    }

    [Fact]
    public void Index_FramesByInteger_BuildAnimatedVisualFromSensedSheet()
    {
        var sheet = SavePainted("frames.png", 48, 16,
            (0, 0, 12, 16),
            (16, 0, 12, 16),
            (32, 0, 12, 16));

        var source = new ImageSource
        {
            FilePath = sheet,
            // No TileSize: integer frames index the sensed regions.
            ["spin"] = { Frames = { 0, 1, 2 }, FrameDuration = TimeSpan.FromSeconds(0.1) },
        };

        var visual = Assert.IsType<AnimatedVisual2D>(source.ToVisual2D());
        var spin = visual.Catalog["spin"];
        Assert.Equal(3, spin.Frames.Length);
        Assert.Equal(new Rect(0, 0, 12, 16), ((ITextureRegion)spin.Frames[0].Texture).Region);
        Assert.Equal(new Rect(32, 0, 12, 16), ((ITextureRegion)spin.Frames[2].Texture).Region);
    }

    [Fact]
    public void Index_AndTileTogether_Throws()
    {
        var sheet = SaveImage("conflict.png", 32, 16);

        var source = new ImageSource { FilePath = sheet, TileSize = (16, 16), Tile = (0, 0), Index = 1 };

        Assert.Throws<InvalidOperationException>(() => source.ToTexture());
    }

    private string SavePainted(string name, int w, int h, params (int X, int Y, int W, int H)[] blocks)
    {
        var path = Path.Combine(_dir, name);
        using var bmp = Bitmap.Create(w, h, PixelFormat.RGBA8888);
        var opaque = new Color(255, 255, 255, 255);
        foreach (var (bx, by, bw, bh) in blocks)
            for (int y = by; y < by + bh; y++)
                for (int x = bx; x < bx + bw; x++)
                    bmp.SetPixel(x, y, opaque);
        bmp.Save(path);
        return path;
    }
}

