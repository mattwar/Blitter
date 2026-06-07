namespace Blitter.Blocks3D;

/// <summary>
/// Sparse <see cref="IVoxelWorld"/> backed by per-chunk <see cref="int"/>
/// arrays in a dictionary keyed by <see cref="ChunkCoord"/>. Chunks are
/// generated on demand via an <see cref="IVoxelGenerator"/> the first
/// time any cell within them is touched. Unloaded chunks read as air.
/// </summary>
public sealed class SparseVoxelWorld : IVoxelWorld
{
    private readonly Dictionary<ChunkCoord, int[]> _chunks = new();
    private readonly IVoxelGenerator _generator;

    public SparseVoxelWorld(
        VoxelPalette palette,
        IVoxelGenerator generator,
        int chunkCellsX = 16,
        int chunkCellsY = 64,
        int chunkCellsZ = 16)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(generator);
        if (chunkCellsX <= 0 || chunkCellsY <= 0 || chunkCellsZ <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkCellsX), "Chunk cell dimensions must be positive.");

        Palette = palette;
        _generator = generator;
        ChunkCellsX = chunkCellsX;
        ChunkCellsY = chunkCellsY;
        ChunkCellsZ = chunkCellsZ;
    }

    /// <inheritdoc/>
    public VoxelPalette Palette { get; }

    /// <summary>Chunk dimension along X, in cells.</summary>
    public int ChunkCellsX { get; }
    /// <summary>Chunk dimension along Y, in cells.</summary>
    public int ChunkCellsY { get; }
    /// <summary>Chunk dimension along Z, in cells.</summary>
    public int ChunkCellsZ { get; }

    /// <inheritdoc/>
    public event EventHandler<VoxelChangeEventArgs>? VoxelsChanged;

    /// <inheritdoc/>
    public int GetVoxel(int x, int y, int z)
    {
        var coord = WorldToChunk(x, y, z);
        if (!_chunks.TryGetValue(coord, out var cells))
            return 0;
        var lx = x - coord.X * ChunkCellsX;
        var ly = y - coord.Y * ChunkCellsY;
        var lz = z - coord.Z * ChunkCellsZ;
        return cells[Index(lx, ly, lz)];
    }

    /// <inheritdoc/>
    public bool SetVoxel(int x, int y, int z, int id)
    {
        var coord = WorldToChunk(x, y, z);
        var cells = EnsureChunkInternal(in coord);
        var lx = x - coord.X * ChunkCellsX;
        var ly = y - coord.Y * ChunkCellsY;
        var lz = z - coord.Z * ChunkCellsZ;
        var i = Index(lx, ly, lz);
        if (cells[i] == id)
            return false;
        cells[i] = id;
        VoxelsChanged?.Invoke(this, VoxelChangeEventArgs.Single(x, y, z));
        return true;
    }

    /// <summary>
    /// Loads (generates if missing) the chunk at <paramref name="coord"/>.
    /// Returns <c>true</c> if a generation happened, <c>false</c> if the
    /// chunk was already loaded.
    /// </summary>
    public bool EnsureChunk(in ChunkCoord coord)
    {
        if (_chunks.ContainsKey(coord))
            return false;
        var cells = new int[ChunkCellsX * ChunkCellsY * ChunkCellsZ];
        _generator.Generate(coord, ChunkCellsX, ChunkCellsY, ChunkCellsZ, cells);
        _chunks[coord] = cells;
        return true;
    }

    /// <summary>
    /// Drops the chunk at <paramref name="coord"/> from storage. Reads of
    /// any cell within it will return air until the chunk is regenerated.
    /// No <see cref="VoxelsChanged"/> event is raised.
    /// </summary>
    public bool UnloadChunk(in ChunkCoord coord) => _chunks.Remove(coord);

    /// <summary>
    /// True if the chunk at <paramref name="coord"/> is currently loaded.
    /// </summary>
    public bool IsChunkLoaded(in ChunkCoord coord) => _chunks.ContainsKey(coord);

    /// <summary>
    /// World-voxel coordinate to chunk-coord conversion. Uses floored
    /// division so negative world coords map correctly.
    /// </summary>
    public ChunkCoord WorldToChunk(int x, int y, int z) =>
        new(FloorDiv(x, ChunkCellsX), FloorDiv(y, ChunkCellsY), FloorDiv(z, ChunkCellsZ));

    private int[] EnsureChunkInternal(in ChunkCoord coord)
    {
        if (!_chunks.TryGetValue(coord, out var cells))
        {
            cells = new int[ChunkCellsX * ChunkCellsY * ChunkCellsZ];
            _generator.Generate(coord, ChunkCellsX, ChunkCellsY, ChunkCellsZ, cells);
            _chunks[coord] = cells;
        }
        return cells;
    }

    private int Index(int lx, int ly, int lz) => (lz * ChunkCellsY + ly) * ChunkCellsX + lx;

    // C# / always rounds toward zero; we need toward -inf so that
    // a negative world coord lands in the correct (negative) chunk.
    private static int FloorDiv(int a, int b)
    {
        var q = a / b;
        if ((a ^ b) < 0 && q * b != a)
            q--;
        return q;
    }
}
