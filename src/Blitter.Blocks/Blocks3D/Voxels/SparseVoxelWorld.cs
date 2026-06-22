namespace Blitter.Blocks3D;

/// <summary>
/// Sparse <see cref="IVoxelWorld"/> backed by per-chunk <see cref="int"/>
/// arrays in a dictionary keyed by <see cref="ChunkCoord"/>. Chunks are
/// generated on demand via an <see cref="IVoxelGenerator"/> the first
/// time any voxel within them is touched. Unloaded chunks read as air.
/// </summary>
public sealed class SparseVoxelWorld : IVoxelWorld
{
    private readonly Dictionary<ChunkCoord, int[]> _chunks = new();
    private readonly List<ChunkCoord> _trimScratch = new();
    private readonly IVoxelGenerator _generator;
    private VoxelInfo[]? _genScratch;

    public SparseVoxelWorld(
        VoxelCatalog catalog,
        IVoxelGenerator generator,
        ChunkSize? size = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(generator);

        Catalog = catalog;
        _generator = generator;
        Size = size ?? ChunkSize.Default;
    }

    /// <inheritdoc/>
    public VoxelCatalog Catalog { get; }

    /// <summary>Chunk dimensions, in voxels.</summary>
    public ChunkSize Size { get; }

    /// <inheritdoc/>
    public event VoxelsChangedHandler? VoxelsChanged;

    /// <inheritdoc/>
    public VoxelInfo GetVoxel(in VoxelCoord coord)
    {
        var (x, y, z) = coord;
        var chunk = WorldToChunk(x, y, z);
        if (!_chunks.TryGetValue(chunk, out var voxels))
            return default;
        var lx = x - chunk.X * Size.X;
        var ly = y - chunk.Y * Size.Y;
        var lz = z - chunk.Z * Size.Z;
        return new VoxelInfo(Catalog[voxels[Size.IndexOf(lx, ly, lz)]]);
    }

    /// <inheritdoc/>
    public bool SetVoxel(in VoxelCoord coord, in VoxelInfo voxel)
    {
        var id = Catalog.IndexOf(voxel.Type);
        var (x, y, z) = coord;
        var chunk = WorldToChunk(x, y, z);
        var voxels = EnsureChunkInternal(in chunk);
        var lx = x - chunk.X * Size.X;
        var ly = y - chunk.Y * Size.Y;
        var lz = z - chunk.Z * Size.Z;
        var i = Size.IndexOf(lx, ly, lz);
        if (voxels[i] == id)
            return false;
        voxels[i] = id;
        VoxelsChanged?.Invoke(this, VoxelBox.Single(coord));
        return true;
    }

    /// <summary>
    /// Loads (generates if missing) the chunk at <paramref name="coord"/>.
    /// Returns <c>true</c> if a generation happened, <c>false</c> if the
    /// chunk was already loaded. A generation raises <see cref="VoxelsChanged"/>
    /// for the whole chunk so consumers re-derive anything reading those
    /// freshly materialized voxels.
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
    /// any voxel within it will return air until the chunk is regenerated.
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
            int x0 = coord.X * Size.X, x1 = x0 + Size.X - 1;
            int y0 = coord.Y * Size.Y, y1 = y0 + Size.Y - 1;
            int z0 = coord.Z * Size.Z, z1 = z0 + Size.Z - 1;
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
        new(FloorDiv(x, Size.X), FloorDiv(y, Size.Y), FloorDiv(z, Size.Z));

    private int[] EnsureChunkInternal(in ChunkCoord coord)
    {
        if (!_chunks.TryGetValue(coord, out var voxels))
            voxels = Generate(in coord);
        return voxels;
    }

    // Allocates, generates, stores, and announces the chunk's whole voxel
    // extent via VoxelsChanged so consumers re-derive against the newly
    // materialized voxels (which may overlap regions they already meshed
    // against air).
    private int[] Generate(in ChunkCoord coord)
    {
        int count = Size.VoxelCount;

        // Reused scratch the generator fills with VoxelInfo, so generators
        // never see how the world packs voxels. Cleared to air each pass.
        var scratch = _genScratch ??= new VoxelInfo[count];
        Array.Clear(scratch);
        int x0 = coord.X * Size.X, y0 = coord.Y * Size.Y, z0 = coord.Z * Size.Z;
        var bounds = new VoxelBox(
            x0, y0, z0,
            x0 + Size.X - 1, y0 + Size.Y - 1, z0 + Size.Z - 1);
        _generator.Generate(new VoxelBuffer(scratch, bounds));

        // Pack the generated types into the world's compact index storage.
        var voxels = new int[count];
        for (int i = 0; i < count; i++)
            voxels[i] = Catalog.IndexOf(scratch[i].Type);

        _chunks[coord] = voxels;
        VoxelsChanged?.Invoke(this, bounds);
        return voxels;
    }

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
