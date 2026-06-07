using System.Numerics;

namespace Blitter.Blocks3D;

/// <summary>
/// Bridges a <see cref="SparseVoxelWorld"/> into the chunked playfield
/// layer. Each generated <see cref="Chunk3D"/> owns one
/// <see cref="VoxelChunkBarrier3D"/> covering the matching voxel
/// chunk; the playfield drives streaming via its
/// <see cref="ChunkedPlayField3D.MinChunk"/> / <see cref="ChunkedPlayField3D.MaxChunk"/>
/// range.
/// </summary>
public class VoxelChunkSource3D : GeneratedChunkSource3D
{
    private readonly Vector3 _cellSize;

    public VoxelChunkSource3D(SparseVoxelWorld world, Vector3 cellSize)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (cellSize.X <= 0f || cellSize.Y <= 0f || cellSize.Z <= 0f)
            throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size components must be positive.");

        World = world;
        _cellSize = cellSize;
        ChunkSize = new Vector3(
            world.ChunkCellsX * cellSize.X,
            world.ChunkCellsY * cellSize.Y,
            world.ChunkCellsZ * cellSize.Z);
    }

    /// <summary>Voxel storage backing every chunk this source produces.</summary>
    public SparseVoxelWorld World { get; }

    /// <summary>World units per voxel cell along each axis.</summary>
    public Vector3 CellSize => _cellSize;

    /// <inheritdoc/>
    public override Vector3 ChunkSize { get; }

    /// <inheritdoc/>
    protected override Chunk3D? GenerateChunk(in ChunkCoord coord)
    {
        World.EnsureChunk(in coord);
        var grid = new VoxelChunkGrid(
            World, coord,
            World.ChunkCellsX, World.ChunkCellsY, World.ChunkCellsZ,
            _cellSize);
        var barrier = new VoxelChunkBarrier3D(grid);
        var chunk = new Chunk3D(this, coord);
        chunk.AddBarrier(barrier);
        return chunk;
    }

    /// <inheritdoc/>
    protected override void OnChunkUnloaded(Chunk3D chunk)
    {
        // Drop the underlying voxel array too so unbounded walking
        // doesn't leak memory. A revisit will regenerate the chunk
        // deterministically from the same generator inputs.
        World.UnloadChunk(chunk.Coord);
    }
}
