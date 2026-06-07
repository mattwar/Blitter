namespace Blitter.Tests;

public class VoxelPaletteTests
{
    private static VoxelType Stone(int id = 1) =>
        new() { Id = id, Name = "stone" };

    [Fact]
    public void NewPalette_ContainsAirAtIdZero()
    {
        var palette = new VoxelPalette();
        Assert.Equal(1, palette.Count);
        Assert.True(palette.IsAir(0));
        Assert.Same(VoxelType.Air, palette[0]);
    }

    [Fact]
    public void Add_RegistersByIdAndName()
    {
        var palette = new VoxelPalette();
        var stone = palette.Add(Stone());

        Assert.Equal(2, palette.Count);
        Assert.Same(stone, palette[1]);
        Assert.Equal(1, palette.IdOf("stone"));
    }

    [Fact]
    public void Add_DuplicateId_Throws()
    {
        var palette = new VoxelPalette();
        palette.Add(new VoxelType { Id = 1, Name = "a" });
        Assert.Throws<ArgumentException>(() => palette.Add(new VoxelType { Id = 1, Name = "b" }));
    }

    [Fact]
    public void Add_DuplicateName_ThrowsAndRollsBack()
    {
        var palette = new VoxelPalette();
        palette.Add(new VoxelType { Id = 1, Name = "stone" });
        Assert.Throws<ArgumentException>(() => palette.Add(new VoxelType { Id = 2, Name = "stone" }));

        // Rolled back: id 2 was not left registered.
        Assert.Equal(2, palette.Count);
        Assert.Same(VoxelType.Air, palette[2]);
    }

    [Fact]
    public void Add_NegativeId_Throws()
    {
        var palette = new VoxelPalette();
        Assert.Throws<ArgumentException>(() => palette.Add(new VoxelType { Id = -1 }));
    }

    [Fact]
    public void Indexer_UnknownId_ReturnsAir()
    {
        var palette = new VoxelPalette();
        Assert.Same(VoxelType.Air, palette[999]);
    }

    [Fact]
    public void IdOf_UnknownName_Throws()
    {
        var palette = new VoxelPalette();
        Assert.Throws<KeyNotFoundException>(() => palette.IdOf("missing"));
    }

    [Fact]
    public void IsOpaque_DelegatesToType()
    {
        var palette = new VoxelPalette();
        palette.Add(new VoxelType { Id = 1, Name = "glass", IsOpaque = false });
        palette.Add(new VoxelType { Id = 2, Name = "stone", IsOpaque = true });

        Assert.False(palette.IsOpaque(1));
        Assert.True(palette.IsOpaque(2));
        Assert.False(palette.IsOpaque(0)); // air
    }
}
