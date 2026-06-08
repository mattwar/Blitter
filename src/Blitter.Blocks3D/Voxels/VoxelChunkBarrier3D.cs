using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// <see cref="Barrier3D"/> wrapping one chunk of voxel data. Hit shape
/// and visual are both backed by the same <see cref="VoxelChunkGrid"/>
/// so collision and rendering see the same cells. The barrier sits at
/// the chunk's world-origin corner; hit shape and draw come from the
/// base class's <see cref="Barrier3D.Visual"/> path.
/// </summary>
internal sealed class VoxelChunkBarrier3D : Barrier3D
{
    private readonly VoxelChunkGrid _grid;
    private readonly VoxelHitShape3D _hitShape;
    private readonly VoxelChunkVisual3D _visual;

    public VoxelChunkBarrier3D(VoxelChunkGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        _grid = grid;
        _hitShape = new VoxelHitShape3D(grid);
        _visual = new VoxelChunkVisual3D(grid, _hitShape);
        Position = grid.WorldOrigin;
        Visual = _visual;
    }

    /// <summary>Shared per-chunk voxel data this barrier reads from.</summary>
    public VoxelChunkGrid Grid => _grid;

    /// <summary>Per-cell hit shape backed by the same grid as the mesh.</summary>
    public VoxelHitShape3D VoxelHitShape => _hitShape;

    /// <summary>Bucketed mesh visual built from the same grid as the hit shape.</summary>
    public VoxelChunkVisual3D VoxelVisual => _visual;
}
