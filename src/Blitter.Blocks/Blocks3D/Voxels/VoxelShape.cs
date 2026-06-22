namespace Blitter.Blocks3D;

/// <summary>
/// Represents the geometry of one kind of voxel.
/// </summary>
public abstract class VoxelShape
{
    /// <summary>
    /// True when this shape completely fills its voxel as a solid unit cube.
    /// </summary>
    public abstract bool FillsVoxel { get; }

    /// <summary>
    /// Adds this voxel's visible geometry to <paramref name="builder"/>,
    /// using <paramref name="context"/> for the voxel's location, size,
    /// and neighbor occlusion.
    /// </summary>
    internal abstract void Build(in VoxelMeshContext context, IChunkMeshBuilder builder);
}

/// <summary>
/// A voxel with no geometry and no collision — air or empty space.
/// </summary>
public sealed class EmptyVoxelShape : VoxelShape
{
    /// <summary>The shared empty shape.</summary>
    public static readonly EmptyVoxelShape Instance = new();

    private EmptyVoxelShape() { }

    /// <inheritdoc/>
    public override bool FillsVoxel => false;

    /// <inheritdoc/>
    internal override void Build(in VoxelMeshContext context, IChunkMeshBuilder builder) { }
}
