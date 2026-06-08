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
    private readonly List<ChunkCoord> _trimScratch = new();
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
    public event VoxelsChangedHandler? VoxelsChanged;

    /// <inheritdoc/>
    public int GetVoxel(VoxelCoord coord)
    {
        var (x, y, z) = coord;
        var chunk = WorldToChunk(x, y, z);
        if (!_chunks.TryGetValue(chunk, out var cells))
            return 0;
        var lx = x - chunk.X * ChunkCellsX;
        var ly = y - chunk.Y * ChunkCellsY;
        var lz = z - chunk.Z * ChunkCellsZ;
        return cells[Index(lx, ly, lz)];
    }

    /// <inheritdoc/>
    public bool SetVoxel(VoxelCoord coord, int id)
    {
        var (x, y, z) = coord;
        var chunk = WorldToChunk(x, y, z);
        var cells = EnsureChunkInternal(in chunk);
        var lx = x - chunk.X * ChunkCellsX;
        var ly = y - chunk.Y * ChunkCellsY;
        var lz = z - chunk.Z * ChunkCellsZ;
        var i = Index(lx, ly, lz);
        if (cells[i] == id)
            return false;
        cells[i] = id;
        VoxelsChanged?.Invoke(this, VoxelBox.Single(coord));
        return true;
    }

    /// <summary>
    /// Loads (generates if missing) the chunk at <paramref name="coord"/>.
    /// Returns <c>true</c> if a generation happened, <c>false</c> if the
    /// chunk was already loaded. A generation raises <see cref="VoxelsChanged"/>
    /// for the whole chunk so consumers re-derive anything reading those
    /// freshly materialized cells.
    /// </summary>
    public bool EnsureChunk(in ChunkCoord coord)
    {
        if (_chunks.ContainsKey(coord))
            return false;
        Generate(in coord);
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

    /// <inheritdoc/>
    public void EnsureVoxels(in VoxelBox range)
    {
        var min = range.Min;
        var max = range.Max;
        if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
            return;
        var lo = WorldToChunk(min.X, min.Y, min.Z);
        var hi = WorldToChunk(max.X, max.Y, max.Z);
        for (int cz = lo.Z; cz <= hi.Z; cz++)
        for (int cy = lo.Y; cy <= hi.Y; cy++)
        for (int cx = lo.X; cx <= hi.X; cx++)
        {
            var coord = new ChunkCoord(cx, cy, cz);
            if (!_chunks.ContainsKey(coord))
                Generate(in coord);
        }
    }

    /// <inheritdoc/>
    public void TrimVoxelsOutside(in VoxelBox range)
    {
        if (_chunks.Count == 0)
            return;
        var min = range.Min;
        var max = range.Max;
        _trimScratch.Clear();
        foreach (var coord in _chunks.Keys)
        {
            // Voxel extent of this chunk, inclusive.
            int x0 = coord.X * ChunkCellsX, x1 = x0 + ChunkCellsX - 1;
            int y0 = coord.Y * ChunkCellsY, y1 = y0 + ChunkCellsY - 1;
            int z0 = coord.Z * ChunkCellsZ, z1 = z0 + ChunkCellsZ - 1;
            bool intersects =
                x1 >= min.X && x0 <= max.X &&
                y1 >= min.Y && y0 <= max.Y &&
                z1 >= min.Z && z0 <= max.Z;
            if (!intersects)
                _trimScratch.Add(coord);
        }
        for (int i = 0; i < _trimScratch.Count; i++)
            _chunks.Remove(_trimScratch[i]);
    }

    /// <summary>
    /// World-voxel coordinate to chunk-coord conversion. Uses floored
    /// division so negative world coords map correctly.
    /// </summary>
    public ChunkCoord WorldToChunk(int x, int y, int z) =>
        new(FloorDiv(x, ChunkCellsX), FloorDiv(y, ChunkCellsY), FloorDiv(z, ChunkCellsZ));

    private int[] EnsureChunkInternal(in ChunkCoord coord)
    {
        if (!_chunks.TryGetValue(coord, out var cells))
            cells = Generate(in coord);
        return cells;
    }

    // Allocates, generates, stores, and announces the chunk's whole voxel
    // extent via VoxelsChanged so consumers re-derive against the newly
    // materialized cells (which may overlap regions they already meshed
    // against air).
    private int[] Generate(in ChunkCoord coord)
    {
        var cells = new int[ChunkCellsX * ChunkCellsY * ChunkCellsZ];
        _generator.Generate(coord, ChunkCellsX, ChunkCellsY, ChunkCellsZ, cells);
        _chunks[coord] = cells;
        int x0 = coord.X * ChunkCellsX, y0 = coord.Y * ChunkCellsY, z0 = coord.Z * ChunkCellsZ;
        VoxelsChanged?.Invoke(this, new VoxelBox(
            x0, y0, z0,
            x0 + ChunkCellsX - 1, y0 + ChunkCellsY - 1, z0 + ChunkCellsZ - 1));
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
