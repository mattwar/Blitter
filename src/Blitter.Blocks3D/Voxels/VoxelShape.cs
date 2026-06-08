namespace Blitter.Blocks3D;

/// <summary>
/// The geometry of one kind of voxel: the faces or triangles it adds to
/// a chunk mesh, and how solid it is for collision. Assigned to
/// <see cref="VoxelType.Shape"/>. Pick from the built-in shapes —
/// <see cref="CubeVoxelShape"/> for a full textured cube or
/// <see cref="EmptyVoxelShape"/> for air.
/// </summary>
public abstract class VoxelShape
{
    /// <summary>
    /// True when this shape completely fills its cell as a solid unit
    /// cube. Collision contributes one full-cell box per filled cell;
    /// empty (and, later, partial) shapes contribute none.
    /// </summary>
    public abstract bool FillsCell { get; }

    /// <summary>
    /// Adds this cell's visible geometry to <paramref name="builder"/>,
    /// using <paramref name="context"/> for the cell's location, size,
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
    public override bool FillsCell => false;

    /// <inheritdoc/>
    internal override void Build(in VoxelMeshContext context, IChunkMeshBuilder builder) { }
}
