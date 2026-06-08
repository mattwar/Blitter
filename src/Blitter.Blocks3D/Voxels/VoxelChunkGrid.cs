using System.Numerics;

namespace Blitter.Blocks3D;

/// <summary>
/// One chunk's worth of voxel data, expressed as a live view onto an
/// <see cref="IVoxelWorld"/>. Shared by the chunk's hit shape and
/// mesher so collision and rendering read the same cells.
/// Reads are forwarded straight to the world; neighbor lookups across
/// chunk edges work for free because the world is OOB-safe.
/// </summary>
internal sealed class VoxelChunkGrid
{
    public VoxelChunkGrid(
        IVoxelWorld world,
        ChunkCoord coord,
        int cellsX,
        int cellsY,
        int cellsZ,
        Vector3 cellSize)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (cellsX <= 0 || cellsY <= 0 || cellsZ <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellsX), "Chunk cell dimensions must be positive.");
        if (cellSize.X <= 0f || cellSize.Y <= 0f || cellSize.Z <= 0f)
            throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size components must be positive.");

        World = world;
        Coord = coord;
        CellsX = cellsX;
        CellsY = cellsY;
        CellsZ = cellsZ;
        CellSize = cellSize;
        OriginCellX = coord.X * cellsX;
        OriginCellY = coord.Y * cellsY;
        OriginCellZ = coord.Z * cellsZ;
        WorldOrigin = new Vector3(OriginCellX, OriginCellY, OriginCellZ) * cellSize;
    }

    /// <summary>The voxel world this chunk reads from.</summary>
    public IVoxelWorld World { get; }

    /// <summary>Chunk coordinate within its source.</summary>
    public ChunkCoord Coord { get; }

    /// <summary>Cells along the X axis.</summary>
    public int CellsX { get; }
    /// <summary>Cells along the Y axis.</summary>
    public int CellsY { get; }
    /// <summary>Cells along the Z axis.</summary>
    public int CellsZ { get; }

    /// <summary>World units per cell along each axis.</summary>
    public Vector3 CellSize { get; }

    /// <summary>World-voxel coordinate of the chunk's local (0,0,0) cell.</summary>
    public int OriginCellX { get; }
    public int OriginCellY { get; }
    public int OriginCellZ { get; }

    /// <summary>World-space position of the chunk's local origin corner.</summary>
    public Vector3 WorldOrigin { get; }

    /// <summary>Palette of the underlying world.</summary>
    public VoxelPalette Palette => World.Palette;

    /// <summary>
    /// Change stamp for this chunk's view of the world. Bumped (by the
    /// owning <see cref="VoxelChunkSource3D"/>, in response to the world's
    /// <see cref="IVoxelWorld.VoxelsChanged"/>) whenever a voxel this
    /// chunk's mesh or collision reads — its own cells or the one-cell
    /// skirt across each face — changes. Derived data snapshots the stamp
    /// and rebuilds when it no longer matches. Only equality is defined.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>Advances <see cref="Version"/> to mark this chunk's
    /// derived data (mesh, collision band) stale.</summary>
    internal void BumpVersion() => Version++;

    /// <summary>
    /// Voxel id at chunk-local cell <paramref name="x"/>,
    /// <paramref name="y"/>, <paramref name="z"/>. Coordinates outside
    /// the chunk are forwarded to the world, which returns air for
    /// cells beyond the world's own bounds.
    /// </summary>
    public int GetVoxel(int x, int y, int z) =>
        World.GetVoxel(new VoxelCoord(OriginCellX + x, OriginCellY + y, OriginCellZ + z));
}
