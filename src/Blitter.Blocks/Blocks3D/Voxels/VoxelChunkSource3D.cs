using System.Numerics;

namespace Blitter.Blocks3D;

/// <summary>
/// Bridges an <see cref="IVoxelWorld"/> into the chunked playfield layer.
/// </summary>
public class VoxelChunkSource3D : ChunkSource3D
{
    private readonly Vector3 _cellSize;
    private readonly int _cellsX;
    private readonly int _cellsY;
    private readonly int _cellsZ;

    // Live grids keyed by playfield chunk coord, so a voxel change can be
    // routed to the chunks that read those cells.
    private readonly Dictionary<ChunkCoord, VoxelChunkGrid> _grids = new();

    public VoxelChunkSource3D(
        IVoxelWorld world,
        Vector3 voxelSize,
        int voxelsPerChunkX,
        int voxelsPerChunkY,
        int voxelsPerChunkZ)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (voxelSize.X <= 0f || voxelSize.Y <= 0f || voxelSize.Z <= 0f)
            throw new ArgumentOutOfRangeException(nameof(voxelSize), "Voxel size components must be positive.");
        if (voxelsPerChunkX <= 0 || voxelsPerChunkY <= 0 || voxelsPerChunkZ <= 0)
            throw new ArgumentOutOfRangeException(nameof(voxelsPerChunkX), "Voxels per chunk must be positive.");

        World = world;
        _cellSize = voxelSize;
        _cellsX = voxelsPerChunkX;
        _cellsY = voxelsPerChunkY;
        _cellsZ = voxelsPerChunkZ;
        ChunkSize = new Vector3(voxelsPerChunkX * voxelSize.X, voxelsPerChunkY * voxelSize.Y, voxelsPerChunkZ * voxelSize.Z);

        World.VoxelsChanged += OnVoxelsChanged;
    }

    /// <summary>Voxel storage backing every chunk this source produces.</summary>
    public IVoxelWorld World { get; }

    /// <summary>World units per voxel along each axis.</summary>
    public Vector3 VoxelSize => _cellSize;

    /// <summary>Voxels per playfield chunk along the X axis.</summary>
    public int VoxelsPerChunkX => _cellsX;
    /// <summary>Voxels per playfield chunk along the Y axis.</summary>
    public int VoxelsPerChunkY => _cellsY;
    /// <summary>Voxels per playfield chunk along the Z axis.</summary>
    public int VoxelsPerChunkZ => _cellsZ;

    /// <inheritdoc/>
    public override Vector3 ChunkSize { get; }

    /// <inheritdoc/>
    protected override Chunk3D? CreateChunk(in ChunkCoord coord)
    {
        // Materialize exactly the voxels this playfield chunk spans. The
        // world may generate more (its chunks can be larger/misaligned);
        // it announces whatever it produced via VoxelsChanged, which our
        // handler routes to any already-loaded chunks that overlap.
        int x0 = coord.X * _cellsX, y0 = coord.Y * _cellsY, z0 = coord.Z * _cellsZ;
        World.EnsureVoxels(new VoxelBox(x0, y0, z0, x0 + _cellsX - 1, y0 + _cellsY - 1, z0 + _cellsZ - 1));

        var grid = new VoxelChunkGrid(World, coord, _cellsX, _cellsY, _cellsZ, _cellSize);
        var barrier = new VoxelChunkBarrier3D(grid);
        var chunk = new Chunk3D(this, coord);
        chunk.AddBarrier(barrier);
        // Registered after EnsureVoxels so the new grid isn't bumped by
        // its own generation event; it starts unbuilt and meshes anyway.
        _grids[coord] = grid;
        return chunk;
    }

    /// <inheritdoc/>
    protected override bool PoolsChunks => true;

    /// <inheritdoc/>
    protected override Chunk3D? ReinitializeChunk(Chunk3D chunk, in ChunkCoord coord)
    {
        // The pooled chunk kept its single VoxelChunkBarrier3D (and the
        // grid + visual + hit shape hanging off it). Retarget the grid to
        // the new coord, reposition and reset the barrier, materialize the
        // new region, and re-route change notifications to it. The mesh and
        // collision buffers are reused in place; only the contents change.
        var barrier = (VoxelChunkBarrier3D)chunk.Barriers[0];
        var grid = barrier.Grid;
        grid.Reinitialize(coord);
        barrier.ResetForReuse();

        int x0 = coord.X * _cellsX, y0 = coord.Y * _cellsY, z0 = coord.Z * _cellsZ;
        World.EnsureVoxels(new VoxelBox(x0, y0, z0, x0 + _cellsX - 1, y0 + _cellsY - 1, z0 + _cellsZ - 1));

        _grids[coord] = grid;
        return chunk;
    }

    /// <inheritdoc/>
    protected override void OnChunkUnloaded(IChunk3D chunk)
    {
        // Stop routing changes to a chunk we've dropped. The visual and
        // hit shape poll their grid's version rather than subscribing, so
        // releasing the reference is all that's needed.
        _grids.Remove(chunk.Coord);
    }

    /// <inheritdoc/>
    public override void TrimChunksOutside(ChunkCoord min, ChunkCoord max)
    {
        base.TrimChunksOutside(min, max);
        // Release voxel storage fully outside the active playfield region.
        // Translate the inclusive chunk box to its inclusive voxel box.
        int x0 = min.X * _cellsX, y0 = min.Y * _cellsY, z0 = min.Z * _cellsZ;
        int x1 = (max.X + 1) * _cellsX - 1;
        int y1 = (max.Y + 1) * _cellsY - 1;
        int z1 = (max.Z + 1) * _cellsZ - 1;
        World.TrimVoxelsOutside(new VoxelBox(x0, y0, z0, x1, y1, z1));
    }

    private void OnVoxelsChanged(IVoxelWorld world, in VoxelBox e)
    {
        if (_grids.Count == 0)
            return;

        // A chunk's mesh reads its own cells plus a one-cell skirt across
        // each face (for face culling), so a change at a boundary cell can
        // affect the chunk on either side of that boundary. Expand the
        // changed box by one cell, map its corners to the playfield-chunk
        // coords it covers, and bump exactly those loaded chunks — rather
        // than testing every loaded chunk for intersection.
        int cxMin = FloorDiv(e.Min.X - 1, _cellsX);
        int cxMax = FloorDiv(e.Max.X + 1, _cellsX);
        int cyMin = FloorDiv(e.Min.Y - 1, _cellsY);
        int cyMax = FloorDiv(e.Max.Y + 1, _cellsY);
        int czMin = FloorDiv(e.Min.Z - 1, _cellsZ);
        int czMax = FloorDiv(e.Max.Z + 1, _cellsZ);

        for (int cz = czMin; cz <= czMax; cz++)
        for (int cy = cyMin; cy <= cyMax; cy++)
        for (int cx = cxMin; cx <= cxMax; cx++)
        {
            if (_grids.TryGetValue(new ChunkCoord(cx, cy, cz), out var grid))
                grid.BumpVersion();
        }
    }

    // C# integer division truncates toward zero; we need floor (toward
    // -inf) so a negative voxel coord maps to the correct (negative) chunk.
    private static int FloorDiv(int a, int b)
    {
        int q = a / b;
        if ((a ^ b) < 0 && q * b != a)
            q--;
        return q;
    }
}
