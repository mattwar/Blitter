using Blitter;
using Blitter.Bits;

namespace Blitter.Tests;

public class TextureCatalogTests
{
    private static Bitmap CreateImage(int w = 16, int h = 16) =>
        Bitmap.Create(w, h, PixelFormat.RGBA8888);

    [Fact]
    public void Construct_StoresImageAndSegments()
    {
        var image = CreateImage();
        var rects = new[] { new Rect(0, 0, 8, 8), new Rect(8, 0, 8, 8) };
        using var atlas = TextureCatalog.FromRegions(image, rects);

        Assert.Equal(2, atlas.Count);
        Assert.Equal(rects[0], ((ITextureRegion)atlas[0]).SourceRect);
        Assert.Equal(rects[1], ((ITextureRegion)atlas[1]).SourceRect);
        Assert.Same(image, ((ITextureRegion)atlas[0]).Source);
    }

    [Fact]
    public void Indexer_OutOfRange_Throws()
    {
        using var atlas = TextureCatalog.FromRegions(CreateImage(), [new Rect(0, 0, 4, 4)]);
        Assert.Throws<IndexOutOfRangeException>(() => atlas[1]);
    }

    [Fact]
    public void NameLookup_ReturnsSegment()
    {
        var rects = new[] { new Rect(0, 0, 4, 4), new Rect(4, 0, 4, 4) };
        var names = new Dictionary<string, int> { ["alpha"] = 0, ["beta"] = 1 };
        using var atlas = TextureCatalog.FromRegions(CreateImage(), rects, names);

        Assert.Equal(rects[0], ((ITextureRegion)atlas["alpha"]).SourceRect);
        Assert.Equal(rects[1], ((ITextureRegion)atlas["beta"]).SourceRect);
        Assert.True(atlas.Contains("alpha"));
        Assert.False(atlas.Contains("missing"));
    }

    [Fact]
    public void NameLookup_Missing_Throws()
    {
        var names = new Dictionary<string, int> { ["a"] = 0 };
        using var atlas = TextureCatalog.FromRegions(CreateImage(), [new Rect(0, 0, 4, 4)], names);
        Assert.Throws<KeyNotFoundException>(() => atlas["nope"]);
    }

    [Fact]
    public void NameLookup_WithoutMap_Throws()
    {
        using var atlas = TextureCatalog.FromRegions(CreateImage(), [new Rect(0, 0, 4, 4)]);
        Assert.Throws<InvalidOperationException>(() => atlas["x"]);
    }

    [Fact]
    public void TryGetIndex_Resolves()
    {
        var names = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1 };
        using var atlas = TextureCatalog.FromRegions(
            CreateImage(),
            [new Rect(0, 0, 4, 4), new Rect(4, 0, 4, 4)],
            names);

        Assert.True(atlas.TryGetIndex("b", out var i));
        Assert.Equal(1, i);
        Assert.False(atlas.TryGetIndex("missing", out var j));
        Assert.Equal(-1, j);
    }

    [Fact]
    public void NamesValidated_AgainstSegmentCount()
    {
        var names = new Dictionary<string, int> { ["a"] = 5 };
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TextureCatalog.FromRegions(CreateImage(), [new Rect(0, 0, 4, 4)], names));
    }

    [Fact]
    public void Grid_DerivesCellsFromImageSize()
    {
        // 16x16 image, 4x4 grid -> 4x4 cells
        var image = CreateImage(16, 16);
        using var atlas = TextureCatalog.Grid(image, columns: 4, rows: 4);

        Assert.Equal(16, atlas.Count);
        Assert.Equal(new Rect(0, 0, 4, 4), ((ITextureRegion)atlas[0]).SourceRect);
        Assert.Equal(new Rect(4, 0, 4, 4), ((ITextureRegion)atlas[1]).SourceRect);
        Assert.Equal(new Rect(0, 4, 4, 4), ((ITextureRegion)atlas[4]).SourceRect);
        Assert.Equal(new Rect(12, 12, 4, 4), ((ITextureRegion)atlas[15]).SourceRect);
    }

    [Fact]
    public void Grid_RowMajor()
    {
        using var atlas = TextureCatalog.Grid(CreateImage(12, 8), columns: 3, rows: 2);
        Assert.Equal(new Rect(8, 4, 4, 4), ((ITextureRegion)atlas[5]).SourceRect); // row 1, col 2
    }

    [Fact]
    public void Grid_WithExplicitCellSize_UsesIt()
    {
        using var atlas = TextureCatalog.Grid(CreateImage(20, 20),
            columns: 4, rows: 4, cellWidth: 4, cellHeight: 4);

        Assert.Equal(16, atlas.Count);
        Assert.Equal(new Rect(12, 12, 4, 4), ((ITextureRegion)atlas[15]).SourceRect);
    }

    [Fact]
    public void Grid_InvalidArgs_Throws()
    {
        var img = CreateImage();
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureCatalog.Grid(img, 0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureCatalog.Grid(img, 2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureCatalog.Grid(img, 2, 2, 0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureCatalog.Grid(img, 2, 2, 4, 0));
    }

    [Fact]
    public void Dispose_DefaultOwnsImage()
    {
        var image = CreateImage();
        var atlas = TextureCatalog.FromRegions(image, [new Rect(0, 0, 4, 4)]);
        atlas.Dispose();
        Assert.True(image.IsDisposed);
    }

    [Fact]
    public void Dispose_DoesNotDisposeImageWhenNotOwned()
    {
        var image = CreateImage();
        var atlas = TextureCatalog.FromRegions(image, [new Rect(0, 0, 4, 4)], ownsImage: false);
        atlas.Dispose();
        Assert.False(image.IsDisposed);
        image.Dispose();
    }

    [Fact]
    public void Dispose_Idempotent()
    {
        var image = CreateImage();
        var atlas = TextureCatalog.FromRegions(image, [new Rect(0, 0, 4, 4)]);
        atlas.Dispose();
        atlas.Dispose();
    }

    [Fact]
    public void Sense_DetectsHorizontalStrip()
    {
        var bmp = CreateImage(16, 4);
        FillRect(bmp, 0, 0, 4, 4);
        FillRect(bmp, 5, 0, 4, 4);
        FillRect(bmp, 10, 0, 4, 4);

        using var atlas = TextureCatalog.Sense(bmp);

        Assert.Equal(3, atlas.Count);
        Assert.Equal(new Rect(0, 0, 4, 4), ((ITextureRegion)atlas[0]).SourceRect);
        Assert.Equal(new Rect(5, 0, 4, 4), ((ITextureRegion)atlas[1]).SourceRect);
        Assert.Equal(new Rect(10, 0, 4, 4), ((ITextureRegion)atlas[2]).SourceRect);
    }

    [Fact]
    public void Sense_DetectsGrid_RowMajor()
    {
        var bmp = CreateImage(10, 10);
        FillRect(bmp, 0, 0, 4, 4);
        FillRect(bmp, 6, 0, 4, 4);
        FillRect(bmp, 0, 6, 4, 4);
        FillRect(bmp, 6, 6, 4, 4);

        using var atlas = TextureCatalog.Sense(bmp);

        Assert.Equal(4, atlas.Count);
        Assert.Equal(new Rect(0, 0, 4, 4), ((ITextureRegion)atlas[0]).SourceRect);
        Assert.Equal(new Rect(6, 0, 4, 4), ((ITextureRegion)atlas[1]).SourceRect);
        Assert.Equal(new Rect(0, 6, 4, 4), ((ITextureRegion)atlas[2]).SourceRect);
        Assert.Equal(new Rect(6, 6, 4, 4), ((ITextureRegion)atlas[3]).SourceRect);
    }

    [Fact]
    public void Sense_SkipsEmptyCellsByDefault()
    {
        var bmp = CreateImage(10, 10);
        FillRect(bmp, 0, 0, 4, 4);
        FillRect(bmp, 6, 6, 4, 4);

        using var atlas = TextureCatalog.Sense(bmp);

        Assert.Equal(2, atlas.Count);
        Assert.Equal(new Rect(0, 0, 4, 4), ((ITextureRegion)atlas[0]).SourceRect);
        Assert.Equal(new Rect(6, 6, 4, 4), ((ITextureRegion)atlas[1]).SourceRect);
    }

    [Fact]
    public void Sense_IncludesEmptyCells_WhenOptedIn()
    {
        var bmp = CreateImage(10, 10);
        FillRect(bmp, 0, 0, 4, 4);
        FillRect(bmp, 6, 6, 4, 4);

        using var atlas = TextureCatalog.Sense(bmp, includeEmptyCells: true);

        Assert.Equal(4, atlas.Count);
        Assert.Equal(new Rect(0, 0, 4, 4), ((ITextureRegion)atlas[0]).SourceRect);
        Assert.Equal(new Rect(6, 0, 4, 4), ((ITextureRegion)atlas[1]).SourceRect);
        Assert.Equal(new Rect(0, 6, 4, 4), ((ITextureRegion)atlas[2]).SourceRect);
        Assert.Equal(new Rect(6, 6, 4, 4), ((ITextureRegion)atlas[3]).SourceRect);
    }

    [Fact]
    public void Sense_AllTransparent_ReturnsEmpty()
    {
        var bmp = CreateImage(8, 8);
        using var atlas = TextureCatalog.Sense(bmp);
        Assert.Equal(0, atlas.Count);
    }

    [Fact]
    public void Sense_HonorsAlphaThreshold()
    {
        var bmp = CreateImage(8, 4);
        FillRect(bmp, 0, 0, 4, 4, new Color(255, 255, 255, 128));
        using var sensedAbove = TextureCatalog.Sense(bmp, alphaThreshold: 200);
        Assert.Equal(0, sensedAbove.Count);
        using var sensedBelow = TextureCatalog.Sense(bmp, alphaThreshold: 64);
        Assert.Equal(1, sensedBelow.Count);
        Assert.Equal(new Rect(0, 0, 4, 4), ((ITextureRegion)sensedBelow[0]).SourceRect);
    }

    private static void FillRect(Bitmap bmp, int x, int y, int w, int h) =>
        FillRect(bmp, x, y, w, h, Color.White);

    private static void FillRect(Bitmap bmp, int x, int y, int w, int h, Color color)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                bmp.SetPixel(xx, yy, color);
    }
}
