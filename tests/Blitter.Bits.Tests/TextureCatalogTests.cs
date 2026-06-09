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
        var atlas = TextureCatalog.FromRegions(image, rects);

        Assert.Equal(2, atlas.Count);
        Assert.Equal(rects[0], ((ITextureRegion)atlas[0]).Region);
        Assert.Equal(rects[1], ((ITextureRegion)atlas[1]).Region);
        Assert.Same(image, ((ITextureRegion)atlas[0]).Source);
    }

    [Fact]
    public void Indexer_OutOfRange_Throws()
    {
        var atlas = TextureCatalog.FromRegions(CreateImage(), [new Rect(0, 0, 4, 4)]);
        Assert.Throws<IndexOutOfRangeException>(() => atlas[1]);
    }

    [Fact]
    public void NameLookup_ReturnsSegment()
    {
        var rects = new[] { new Rect(0, 0, 4, 4), new Rect(4, 0, 4, 4) };
        var names = new[] { "alpha", "beta" };
        var atlas = TextureCatalog.FromRegions(CreateImage(), rects, names);

        Assert.Equal(rects[0], ((ITextureRegion)atlas["alpha"]).Region);
        Assert.Equal(rects[1], ((ITextureRegion)atlas["beta"]).Region);
        Assert.True(atlas.Contains("alpha"));
        Assert.False(atlas.Contains("missing"));
    }

    [Fact]
    public void Named_PairsTexturesWithNames()
    {
        var image = CreateImage();
        var a = image.Slice(new Rect(0, 0, 4, 4));
        var b = image.Slice(new Rect(4, 0, 4, 4));
        var atlas = TextureCatalog.Named([(a, "alpha"), (b, "beta")]);

        Assert.Equal(2, atlas.Count);
        Assert.Same(a, atlas["alpha"]);
        Assert.Same(b, atlas["beta"]);
        Assert.Equal(new[] { "alpha", "beta" }, atlas.Names);
    }

    [Fact]
    public void Named_DuplicateNames_Throw()
    {
        var image = CreateImage();
        var a = image.Slice(new Rect(0, 0, 4, 4));
        var b = image.Slice(new Rect(4, 0, 4, 4));
        Assert.Throws<ArgumentException>(
            () => TextureCatalog.Named([(a, "dup"), (b, "dup")]));
    }

    [Fact]
    public void NameLookup_Missing_Throws()
    {
        var names = new[] { "a" };
        var atlas = TextureCatalog.FromRegions(CreateImage(), [new Rect(0, 0, 4, 4)], names);
        Assert.Throws<KeyNotFoundException>(() => atlas["nope"]);
    }

    [Fact]
    public void NameLookup_WithoutMap_Throws()
    {
        var atlas = TextureCatalog.FromRegions(CreateImage(), [new Rect(0, 0, 4, 4)]);
        Assert.Throws<InvalidOperationException>(() => atlas["x"]);
    }

    [Fact]
    public void TryGetIndex_Resolves()
    {
        var names = new[] { "a", "b" };
        var atlas = TextureCatalog.FromRegions(
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
        var names = new[] { "a", "b" };
        Assert.Throws<ArgumentException>(
            () => TextureCatalog.FromRegions(CreateImage(), [new Rect(0, 0, 4, 4)], names));
    }

    [Fact]
    public void DuplicateNames_Throw()
    {
        var names = new[] { "dup", "dup" };
        Assert.Throws<ArgumentException>(
            () => TextureCatalog.FromRegions(
                CreateImage(),
                [new Rect(0, 0, 4, 4), new Rect(4, 0, 4, 4)],
                names));
    }

    [Fact]
    public void PartialNames_NameLeadingEntries()
    {
        var names = new[] { "first" };
        var atlas = TextureCatalog.FromRegions(
            CreateImage(),
            [new Rect(0, 0, 4, 4), new Rect(4, 0, 4, 4)],
            names);

        Assert.True(atlas.TryGetIndex("first", out var i));
        Assert.Equal(0, i);
        Assert.Equal(new[] { "first" }, atlas.Names);
    }

    [Fact]
    public void Grid_DerivesCellsFromImageSize()
    {
        // 16x16 image, 4x4 grid -> 4x4 cells
        var image = CreateImage(16, 16);
        var atlas = TextureCatalog.Grid(image, columns: 4, rows: 4);

        Assert.Equal(16, atlas.Count);
        Assert.Equal(new Rect(0, 0, 4, 4), ((ITextureRegion)atlas[0]).Region);
        Assert.Equal(new Rect(4, 0, 4, 4), ((ITextureRegion)atlas[1]).Region);
        Assert.Equal(new Rect(0, 4, 4, 4), ((ITextureRegion)atlas[4]).Region);
        Assert.Equal(new Rect(12, 12, 4, 4), ((ITextureRegion)atlas[15]).Region);
    }

    [Fact]
    public void Grid_RowMajor()
    {
        var atlas = TextureCatalog.Grid(CreateImage(12, 8), columns: 3, rows: 2);
        Assert.Equal(new Rect(8, 4, 4, 4), ((ITextureRegion)atlas[5]).Region); // row 1, col 2
    }

    [Fact]
    public void Tiles_DerivesCountsFromTileSize()
    {
        var atlas = TextureCatalog.Tiles(CreateImage(20, 20), tileWidth: 4, tileHeight: 4);

        Assert.Equal(25, atlas.Count);
        Assert.Equal(5, atlas.Columns);
        Assert.Equal(5, atlas.Rows);
        Assert.Equal(new Rect(0, 0, 4, 4), ((ITextureRegion)atlas[0]).Region);
        Assert.Equal(new Rect(16, 16, 4, 4), ((ITextureRegion)atlas[24]).Region);
    }

    [Fact]
    public void Tiles_IgnoresTrailingPartialTiles()
    {
        // 22x10 with 4x4 tiles -> 5 columns (20px) and 2 rows (8px); the
        // trailing 2px column and 2px row are ignored.
        var atlas = TextureCatalog.Tiles(CreateImage(22, 10), 4, 4);
        Assert.Equal(5, atlas.Columns);
        Assert.Equal(2, atlas.Rows);
    }

    [Fact]
    public void Tiles_InvalidArgs_Throws()
    {
        var img = CreateImage();
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureCatalog.Tiles(img, 0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureCatalog.Tiles(img, 4, 0));
    }

    [Fact]
    public void Grid_InvalidArgs_Throws()
    {
        var img = CreateImage();
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureCatalog.Grid(img, 0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureCatalog.Grid(img, 2, 0));
    }

    [Fact]
    public void Grid_Shape_ExposesColumnsAndRows()
    {
        var atlas = TextureCatalog.Grid(CreateImage(12, 8), columns: 3, rows: 2);
        Assert.Equal(3, atlas.Columns);
        Assert.Equal(2, atlas.Rows);
    }

    [Fact]
    public void GridIndexer_MapsColumnRowToRowMajorIndex()
    {
        var atlas = TextureCatalog.Grid(CreateImage(12, 8), columns: 3, rows: 2);
        // row 1, col 2 -> linear index 5
        Assert.Same(atlas[5], atlas[2, 1]);
        Assert.Same(atlas[0], atlas[0, 0]);
        Assert.Same(atlas[3], atlas[0, 1]);
    }

    [Fact]
    public void GridIndexer_OutOfRange_Throws()
    {
        var atlas = TextureCatalog.Grid(CreateImage(12, 8), columns: 3, rows: 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => atlas[3, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => atlas[0, 2]);
    }

    [Fact]
    public void FlatCatalog_IsSingleRow()
    {
        var rects = new[] { new Rect(0, 0, 4, 4), new Rect(4, 0, 4, 4), new Rect(8, 0, 4, 4) };
        var atlas = TextureCatalog.FromRegions(CreateImage(), rects);
        Assert.Equal(3, atlas.Columns);
        Assert.Equal(1, atlas.Rows);
        Assert.Same(atlas[2], atlas[2, 0]);
    }

    [Fact]
    public void Constructor_FromTextures_IsSingleRow()
    {
        var textures = new[] { (Texture2D)CreateImage(4, 4), CreateImage(4, 4) };
        var atlas = new TextureCatalog(textures);
        Assert.Equal(2, atlas.Columns);
        Assert.Equal(1, atlas.Rows);
        Assert.Same(atlas[1], atlas[1, 0]);
    }

    [Fact]
    public void EmptyCatalog_GridIndexer_Throws()
    {
        var atlas = new TextureCatalog(ReadOnlySpan<Texture2D>.Empty);
        Assert.Equal(0, atlas.Columns);
        Assert.Equal(0, atlas.Rows);
        Assert.Throws<InvalidOperationException>(() => atlas[0, 0]);
    }

    [Fact]
    public void Sense_DetectsHorizontalStrip()
    {
        var bmp = CreateImage(16, 4);
        FillRect(bmp, 0, 0, 4, 4);
        FillRect(bmp, 5, 0, 4, 4);
        FillRect(bmp, 10, 0, 4, 4);

        var atlas = TextureCatalog.Sense(bmp);

        Assert.Equal(3, atlas.Count);
        Assert.Equal(new Rect(0, 0, 4, 4), ((ITextureRegion)atlas[0]).Region);
        Assert.Equal(new Rect(5, 0, 4, 4), ((ITextureRegion)atlas[1]).Region);
        Assert.Equal(new Rect(10, 0, 4, 4), ((ITextureRegion)atlas[2]).Region);
    }

    [Fact]
    public void Sense_DetectsGrid_RowMajor()
    {
        var bmp = CreateImage(10, 10);
        FillRect(bmp, 0, 0, 4, 4);
        FillRect(bmp, 6, 0, 4, 4);
        FillRect(bmp, 0, 6, 4, 4);
        FillRect(bmp, 6, 6, 4, 4);

        var atlas = TextureCatalog.Sense(bmp);

        Assert.Equal(4, atlas.Count);
        Assert.Equal(new Rect(0, 0, 4, 4), ((ITextureRegion)atlas[0]).Region);
        Assert.Equal(new Rect(6, 0, 4, 4), ((ITextureRegion)atlas[1]).Region);
        Assert.Equal(new Rect(0, 6, 4, 4), ((ITextureRegion)atlas[2]).Region);
        Assert.Equal(new Rect(6, 6, 4, 4), ((ITextureRegion)atlas[3]).Region);
    }

    [Fact]
    public void Sense_SkipsEmptyCellsByDefault()
    {
        var bmp = CreateImage(10, 10);
        FillRect(bmp, 0, 0, 4, 4);
        FillRect(bmp, 6, 6, 4, 4);

        var atlas = TextureCatalog.Sense(bmp);

        Assert.Equal(2, atlas.Count);
        Assert.Equal(new Rect(0, 0, 4, 4), ((ITextureRegion)atlas[0]).Region);
        Assert.Equal(new Rect(6, 6, 4, 4), ((ITextureRegion)atlas[1]).Region);
    }

    [Fact]
    public void Sense_HandlesMisalignedBands()
    {
        // Top band has cells at x in [0,4) and [6,10).
        // Bottom band has a single cell at x in [3,8) — its column
        // range overlaps both top cells. A global column projection
        // would merge everything; per-band projection must not.
        var bmp = CreateImage(10, 10);
        FillRect(bmp, 0, 0, 4, 4);
        FillRect(bmp, 6, 0, 4, 4);
        FillRect(bmp, 3, 6, 5, 4);

        var atlas = TextureCatalog.Sense(bmp);

        Assert.Equal(3, atlas.Count);
        Assert.Equal(new Rect(0, 0, 4, 4), ((ITextureRegion)atlas[0]).Region);
        Assert.Equal(new Rect(6, 0, 4, 4), ((ITextureRegion)atlas[1]).Region);
        Assert.Equal(new Rect(3, 6, 5, 4), ((ITextureRegion)atlas[2]).Region);
    }

    [Fact]
    public void Sense_DropsRegions_BelowMinSize()
    {
        // Three rows: a real sprite row (8x8), a 1-pixel noise stripe,
        // and another real sprite row. Within the bottom row there's
        // a stray 2x8 sliver before the real 8x8 cell.
        var bmp = CreateImage(20, 20);
        FillRect(bmp, 0, 0, 8, 8);
        FillRect(bmp, 0, 10, 1, 1); // noise pixel between bands
        FillRect(bmp, 0, 12, 2, 8); // narrow sliver
        FillRect(bmp, 4, 12, 8, 8); // real cell

        var atlas = TextureCatalog.Sense(bmp, minRegionWidth: 4, minRegionHeight: 4);

        Assert.Equal(2, atlas.Count);
        Assert.Equal(new Rect(0, 0, 8, 8), ((ITextureRegion)atlas[0]).Region);
        Assert.Equal(new Rect(4, 12, 8, 8), ((ITextureRegion)atlas[1]).Region);
    }

    [Fact]
    public void Sense_BridgesSmallGutters()
    {
        // A sprite (8 tall) with a 2-pixel transparent break above
        // a 3-pixel shadow tail. With default minRowGutter=1 the
        // algorithm splits into two bands; with minRowGutter=3 a
        // 2-pixel gap is below threshold so the gap is bridged and
        // we get one region covering both pieces.
        var bmp = CreateImage(8, 16);
        FillRect(bmp, 0, 0, 8, 8);
        FillRect(bmp, 0, 10, 8, 3); // shadow tail after a 2-row gutter

        var split = TextureCatalog.Sense(bmp);
        Assert.Equal(2, split.Count);

        var merged = TextureCatalog.Sense(bmp, minRowGutter: 3);
        Assert.Equal(1, merged.Count);
        Assert.Equal(new Rect(0, 0, 8, 13), ((ITextureRegion)merged[0]).Region);
    }

    [Fact]
    public void Sense_AllTransparent_ReturnsEmpty()
    {
        var bmp = CreateImage(8, 8);
        var atlas = TextureCatalog.Sense(bmp);
        Assert.Equal(0, atlas.Count);
    }

    [Fact]
    public void Sense_HonorsAlphaThreshold()
    {
        var bmp = CreateImage(8, 4);
        FillRect(bmp, 0, 0, 4, 4, new Color(255, 255, 255, 128));
        var sensedAbove = TextureCatalog.Sense(bmp, alphaThreshold: 200);
        Assert.Equal(0, sensedAbove.Count);
        var sensedBelow = TextureCatalog.Sense(bmp, alphaThreshold: 64);
        Assert.Equal(1, sensedBelow.Count);
        Assert.Equal(new Rect(0, 0, 4, 4), ((ITextureRegion)sensedBelow[0]).Region);
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
