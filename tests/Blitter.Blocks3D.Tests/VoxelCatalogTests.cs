namespace Blitter.Tests;

public class VoxelCatalogTests
{
    private static VoxelType Stone() => new() { Name = "stone" };

    [Fact]
    public void NewCatalog_ContainsAirAtIndexZero()
    {
        var catalog = new VoxelCatalog();
        Assert.Equal(1, catalog.Count);
        Assert.Same(VoxelType.Air, catalog[0]);
        Assert.Same(VoxelType.Air, catalog["air"]);
        Assert.Equal(0, catalog.IndexOf(VoxelType.Air));
    }

    [Fact]
    public void Add_RegistersByIndexAndName()
    {
        var catalog = new VoxelCatalog();
        var stone = catalog.Add(Stone());

        Assert.Equal(2, catalog.Count);
        Assert.Same(stone, catalog[1]);
        Assert.Same(stone, catalog["stone"]);
        Assert.Equal(1, catalog.IndexOf(stone));
    }

    [Fact]
    public void Add_StampsDenseSequentialIndices()
    {
        var catalog = new VoxelCatalog();
        var a = catalog.Add(new VoxelType { Name = "a" });
        var b = catalog.Add(new VoxelType { Name = "b" });

        Assert.Equal(1, catalog.IndexOf(a));
        Assert.Equal(2, catalog.IndexOf(b));
    }

    [Fact]
    public void Add_DuplicateName_Throws()
    {
        var catalog = new VoxelCatalog();
        catalog.Add(new VoxelType { Name = "stone" });
        Assert.Throws<ArgumentException>(() => catalog.Add(new VoxelType { Name = "stone" }));
    }

    [Fact]
    public void Add_EmptyName_Throws()
    {
        var catalog = new VoxelCatalog();
        Assert.Throws<ArgumentException>(() => catalog.Add(new VoxelType()));
    }

    [Fact]
    public void Add_TypeAlreadyOwned_Throws()
    {
        var first = new VoxelCatalog();
        var stone = first.Add(Stone());

        var second = new VoxelCatalog();
        Assert.Throws<ArgumentException>(() => second.Add(stone));
    }

    [Fact]
    public void Indexer_UnknownName_Throws()
    {
        var catalog = new VoxelCatalog();
        Assert.Throws<KeyNotFoundException>(() => catalog["missing"]);
    }

    [Fact]
    public void IndexOf_TypeFromAnotherCatalog_Throws()
    {
        var first = new VoxelCatalog();
        var stone = first.Add(Stone());
        var second = new VoxelCatalog();

        Assert.Throws<ArgumentException>(() => second.IndexOf(stone));
    }

    [Fact]
    public void TryGet_AndTryGetIndex()
    {
        var catalog = new VoxelCatalog();
        var stone = catalog.Add(Stone());

        Assert.True(catalog.TryGet("stone", out var t));
        Assert.Same(stone, t);
        Assert.True(catalog.TryGetIndex("stone", out var i));
        Assert.Equal(1, i);

        Assert.False(catalog.TryGet("missing", out _));
        Assert.False(catalog.TryGetIndex("missing", out _));
    }

    [Fact]
    public void Contains_ReportsRegistration()
    {
        var catalog = new VoxelCatalog();
        catalog.Add(Stone());
        Assert.True(catalog.Contains("stone"));
        Assert.False(catalog.Contains("missing"));
    }

    [Fact]
    public void Names_AreInRegistrationOrder()
    {
        var catalog = new VoxelCatalog();
        catalog.Add(new VoxelType { Name = "stone" });
        catalog.Add(new VoxelType { Name = "dirt" });

        Assert.Equal(new[] { "air", "stone", "dirt" }, catalog.Names);
    }

    [Fact]
    public void CollectionInitializer_AddsTypes()
    {
        var catalog = new VoxelCatalog
        {
            new VoxelType { Name = "stone" },
            new VoxelType { Name = "dirt" },
        };

        Assert.Equal(3, catalog.Count);
        Assert.Equal(new[] { "air", "stone", "dirt" }, catalog.Names);
    }
}
