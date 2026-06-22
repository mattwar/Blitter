using System.Numerics;


namespace Blitter.Blocks3D;

/// <summary>
/// Accumulates a voxel chunk's geometry, bucketed by source texture.
/// Voxel shapes add their faces here; the implementation (the chunk
/// visual) turns the buckets into draw-ready meshes.
/// </summary>
internal interface IChunkMeshBuilder
{
    /// <summary>
    /// Adds one quad to the bucket for <paramref name="sourceTexture"/>
    /// (null means untextured). The four vertices wind CCW seen from
    /// outside the cell (<c>v0 → v1 → v2</c> follows the face normal),
    /// so the builder appends triangles <c>v0,v1,v2</c> and <c>v0,v2,v3</c>.
    /// <paramref name="transparency"/> selects how the quad's alpha is
    /// composited (opaque, see-through cutout, or alpha blend).
    /// </summary>
    void AddQuad(
        Texture2D? sourceTexture,
        TransparencyMode transparency,
        in LitTextureVertex3D v0,
        in LitTextureVertex3D v1,
        in LitTextureVertex3D v2,
        in LitTextureVertex3D v3);

    /// <summary>
    /// Adds one triangle to the bucket for <paramref name="sourceTexture"/>
    /// (null means untextured). The three vertices wind CCW seen from
    /// outside the surface (<c>v0 → v1 → v2</c> follows the face normal).
    /// For shapes that aren't built from quads — slopes, smooth
    /// surfaces, imported meshes. <paramref name="transparency"/> selects
    /// the surface's compositing just like <see cref="AddQuad"/>.
    /// </summary>
    void AddTriangle(
        Texture2D? sourceTexture,
        TransparencyMode transparency,
        in LitTextureVertex3D v0,
        in LitTextureVertex3D v1,
        in LitTextureVertex3D v2);
}

/// <summary>
/// What a <see cref="VoxelShape"/> needs while adding a cell's geometry:
/// where the cell sits in the grid, how big it is, and whether each
/// neighbor occludes the touching face.
/// </summary>
internal readonly struct VoxelMeshContext
{
    private static readonly (int DX, int DY, int DZ)[] _faceOffsets =
    {
        (-1, 0, 0), (1, 0, 0), (0, -1, 0), (0, 1, 0), (0, 0, -1), (0, 0, 1),
    };

    private readonly VoxelChunkGrid _grid;

    public VoxelMeshContext(VoxelChunkGrid grid, int x, int y, int z)
    {
        _grid = grid;
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>Cell coordinate within the chunk.</summary>
    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    /// <summary>World-unit size of one cell.</summary>
    public Vector3 CellSize => _grid.CellSize;

    /// <summary>
    /// True when the neighbor across <paramref name="face"/> is opaque,
    /// so a face pointing at it can be culled.
    /// </summary>
    public bool IsNeighborOpaque(VoxelFace face)
    {
        var off = _faceOffsets[(int)face];
        return _grid.GetVoxel(X + off.DX, Y + off.DY, Z + off.DZ).IsOpaque;
    }

    /// <summary>
    /// True when the face toward <paramref name="face"/> can be skipped
    /// for a cell holding <paramref name="ownType"/>: either the
    /// neighbor is opaque, or it's the same voxel type as this one (so two
    /// adjacent panes of the same translucent block don't draw the
    /// doubled interior face between them).
    /// </summary>
    public bool IsNeighborOccluding(VoxelFace face, VoxelType ownType)
    {
        var off = _faceOffsets[(int)face];
        var neighbor = _grid.GetVoxel(X + off.DX, Y + off.DY, Z + off.DZ);
        return ReferenceEquals(neighbor.Type, ownType) || neighbor.IsOpaque;
    }

    /// <summary>The voxel type stored in this cell.</summary>
    public VoxelType Voxel => _grid.GetVoxel(X, Y, Z).Type;
}

/// <summary>
/// Walks a <see cref="VoxelChunkGrid"/> and lets each cell's
/// <see cref="VoxelShape"/> add its geometry to an
/// <see cref="IChunkMeshBuilder"/>.
/// </summary>
internal static class VoxelMesher
{
    /// <summary>
    /// Walks the grid and asks every cell's shape to add its geometry.
    /// </summary>
    public static void Build(VoxelChunkGrid grid, IChunkMeshBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(builder);

        for (int z = 0; z < grid.CellsZ; z++)
        for (int y = 0; y < grid.CellsY; y++)
        for (int x = 0; x < grid.CellsX; x++)
        {
            var type = grid.GetVoxel(x, y, z).Type;
            type.Shape.Build(new VoxelMeshContext(grid, x, y, z), builder);
        }
    }
}

