using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// <see cref="Barrier3D"/> wrapping one chunk of voxel data. The
/// barrier sits at the chunk's world-origin corner; its
/// <see cref="HitShape"/> walks per-cell box primitives drawn from the
/// shared <see cref="VoxelChunkGrid"/>. Rendering is left to a later
/// override once the chunk mesher exists.
/// </summary>
public class VoxelChunkBarrier3D : Barrier3D
{
    private readonly VoxelChunkGrid _grid;
    private readonly VoxelHitShape3D _hitShape;

    public VoxelChunkBarrier3D(VoxelChunkGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        _grid = grid;
        _hitShape = new VoxelHitShape3D(grid);
        Position = grid.WorldOrigin;
    }

    /// <summary>Shared per-chunk voxel data this barrier reads from.</summary>
    public VoxelChunkGrid Grid => _grid;

    /// <summary>Per-cell hit shape backed by the same grid as the eventual mesh.</summary>
    public VoxelHitShape3D VoxelHitShape => _hitShape;

    public override PosedHitShape3D HitShape =>
        new(_hitShape, new Pose3D(Position, Orientation, Scale));

    public override void Draw(Renderer3D renderer)
    {
        // Voxel mesh rendering arrives with the chunk mesher.
    }
}
