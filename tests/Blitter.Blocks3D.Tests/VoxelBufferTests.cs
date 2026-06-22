namespace Blitter.Tests;

public class VoxelBufferTests
{
    private static VoxelCatalog MakeCatalog()
    {
        var catalog = new VoxelCatalog();
        catalog.Add(new VoxelType { Name = "stone" });
        return catalog;
    }

    [Fact]
    public void Indexer_MapsCoordinatesOntoLinearStorage()
    {
        var bounds = new VoxelBox(8, 3, -2, 11, 5, -1); // 4 x 3 x 2
        var storage = new VoxelInfo[4 * 3 * 2];
        var catalog = MakeCatalog();
        var stone = catalog["stone"];

        var buffer = new VoxelBuffer(storage, bounds);
        // Coord (9, 5, -1) -> local (1, 2, 1) -> index (1*3 + 2)*4 + 1.
        buffer[9, 5, -1] = stone;

        Assert.Same(stone, buffer[9, 5, -1].Type);
        Assert.Same(stone, storage[(1 * 3 + 2) * 4 + 1].Type);
    }

    [Fact]
    public void VoxelCoordIndexer_AgreesWithComponentIndexer()
    {
        var bounds = new VoxelBox(8, 3, -2, 11, 5, -1);
        var storage = new VoxelInfo[4 * 3 * 2];
        var stone = MakeCatalog()["stone"];

        var buffer = new VoxelBuffer(storage, bounds);
        buffer[new VoxelCoord(10, 4, -2)] = stone;

        Assert.Same(stone, buffer[10, 4, -2].Type);
    }

    [Fact]
    public void Bounds_ReportsTheRegion()
    {
        var bounds = new VoxelBox(8, 3, -2, 11, 5, -1);
        var buffer = new VoxelBuffer(new VoxelInfo[4 * 3 * 2], bounds);

        Assert.Equal(bounds, buffer.Bounds);
    }

    [Theory]
    [InlineData(7, 3, -2)]  // x below min (8)
    [InlineData(12, 3, -2)] // x above max (11)
    [InlineData(8, 2, -2)]  // y below min (3)
    [InlineData(8, 6, -2)]  // y above max (5)
    [InlineData(8, 3, -3)]  // z below min (-2)
    [InlineData(8, 3, 0)]   // z above max (-1)
    public void Indexer_OutOfBounds_Throws(int x, int y, int z)
    {
        var bounds = new VoxelBox(8, 3, -2, 11, 5, -1);
        var storage = new VoxelInfo[4 * 3 * 2];

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var buffer = new VoxelBuffer(storage, bounds);
            _ = buffer[x, y, z];
        });
    }

    [Fact]
    public void Constructor_RejectsMismatchedSpanLength()
    {
        var bounds = new VoxelBox(0, 0, 0, 3, 2, 1); // 4 x 3 x 2 = 24
        var tooShort = new VoxelInfo[23];

        Assert.Throws<ArgumentException>(() => new VoxelBuffer(tooShort, bounds));
    }
}
