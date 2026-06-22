namespace Blitter.Blocks3D;

/// <summary>
/// Identifies one of the six faces of a voxel in its own local space
/// (before any per-cell orientation is applied). The value order
/// matches the voxel mesher's internal face indexing, so a face index
/// and a <see cref="VoxelFace"/> are interchangeable.
/// </summary>
public enum VoxelFace
{
    /// <summary>The -X face (left side).</summary>
    NegativeX = 0,

    /// <summary>The +X face (right side).</summary>
    PositiveX = 1,

    /// <summary>The -Y face (bottom).</summary>
    NegativeY = 2,

    /// <summary>The +Y face (top).</summary>
    PositiveY = 3,

    /// <summary>The -Z face.</summary>
    NegativeZ = 4,

    /// <summary>The +Z face.</summary>
    PositiveZ = 5,
}
