using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// Receives one quad at a time from <see cref="VoxelMesher"/>. The
/// four vertices are emitted in CCW order when viewed from outside
/// the cell (<c>v0 → v1 → v2</c> winds in the direction of the face
/// normal), so the sink can append two triangles
/// <c>v0,v1,v2</c> and <c>v0,v2,v3</c> directly.
/// </summary>
public interface IVoxelMeshSink
{
    /// <summary>
    /// Routes a quad to the bucket keyed by <paramref name="sourceTexture"/>.
    /// Null means "no texture" — the sink picks a default material.
    /// </summary>
    void EmitQuad(
        Texture2D? sourceTexture,
        in LitTextureVertex3D v0,
        in LitTextureVertex3D v1,
        in LitTextureVertex3D v2,
        in LitTextureVertex3D v3);
}

/// <summary>
/// Builds the visible-face geometry of a <see cref="VoxelChunkGrid"/>
/// into the supplied <see cref="IVoxelMeshSink"/>. Naive face culling
/// only — every face whose neighbor cell is non-opaque is emitted.
/// </summary>
public static class VoxelMesher
{
    private static readonly Vector3[] _faceNormals =
    {
        new(-1f, 0f, 0f), // -X
        new( 1f, 0f, 0f), // +X
        new( 0f,-1f, 0f), // -Y
        new( 0f, 1f, 0f), // +Y
        new( 0f, 0f,-1f), // -Z
        new( 0f, 0f, 1f), // +Z
    };

    // Per face: origin corner (unit-cell coords) and two edge axes.
    // origin + e1 + e2 + (cross e1 e2 = normal) gives an outward-CCW quad.
    private static readonly (Vector3 Origin, Vector3 E1, Vector3 E2)[] _faceCorners =
    {
        (new(0f, 0f, 0f), new(0f, 0f, 1f), new(0f, 1f, 0f)), // -X
        (new(1f, 0f, 0f), new(0f, 1f, 0f), new(0f, 0f, 1f)), // +X
        (new(0f, 0f, 0f), new(1f, 0f, 0f), new(0f, 0f, 1f)), // -Y
        (new(0f, 1f, 0f), new(0f, 0f, 1f), new(1f, 0f, 0f)), // +Y
        (new(0f, 0f, 0f), new(0f, 1f, 0f), new(1f, 0f, 0f)), // -Z
        (new(0f, 0f, 1f), new(1f, 0f, 0f), new(0f, 1f, 0f)), // +Z
    };

    private static readonly (int DX, int DY, int DZ)[] _faceOffsets =
    {
        (-1, 0, 0), (1, 0, 0), (0, -1, 0), (0, 1, 0), (0, 0, -1), (0, 0, 1),
    };

    /// <summary>
    /// Walks the grid and emits one quad per visible face.
    /// </summary>
    public static void Build(VoxelChunkGrid grid, IVoxelMeshSink sink)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(sink);

        var palette = grid.Palette;
        var cellSize = grid.CellSize;

        for (int z = 0; z < grid.CellsZ; z++)
        for (int y = 0; y < grid.CellsY; y++)
        for (int x = 0; x < grid.CellsX; x++)
        {
            var type = palette[grid.GetVoxel(x, y, z)];
            if (type.Shape != VoxelShape.FullBlock)
                continue;

            // Resolve source texture + normalized UV sub-rect once per cell.
            var (sourceTexture, u0, v0, u1, v1) = ResolveUvRect(type.Texture);

            for (int face = 0; face < 6; face++)
            {
                var off = _faceOffsets[face];
                if (palette.IsOpaque(grid.GetVoxel(x + off.DX, y + off.DY, z + off.DZ)))
                    continue;

                EmitFace(sink, sourceTexture, x, y, z, cellSize, face, u0, v0, u1, v1);
            }
        }
    }

    private static void EmitFace(
        IVoxelMeshSink sink,
        Texture2D? sourceTexture,
        int cellX, int cellY, int cellZ,
        Vector3 cellSize,
        int face,
        float u0, float v0, float u1, float v1)
    {
        var (origin, e1, e2) = _faceCorners[face];
        var normal = _faceNormals[face];
        var cellOrigin = new Vector3(cellX, cellY, cellZ) * cellSize;

        var c0 = cellOrigin + origin * cellSize;
        var c1 = c0 + e1 * cellSize;
        var c2 = c1 + e2 * cellSize;
        var c3 = c0 + e2 * cellSize;

        var v0v = new LitTextureVertex3D(c0, normal, new Vector2(u0, v1));
        var v1v = new LitTextureVertex3D(c1, normal, new Vector2(u1, v1));
        var v2v = new LitTextureVertex3D(c2, normal, new Vector2(u1, v0));
        var v3v = new LitTextureVertex3D(c3, normal, new Vector2(u0, v0));
        sink.EmitQuad(sourceTexture, in v0v, in v1v, in v2v, in v3v);
    }

    private static (Texture2D? Source, float U0, float V0, float U1, float V1) ResolveUvRect(Texture2D? texture)
    {
        if (texture is null)
            return (null, 0f, 0f, 0f, 0f);
        if (texture is ITextureRegion region)
        {
            var r = region.Region;
            var src = region.Source;
            float sw = src.Width;
            float sh = src.Height;
            return (src,
                r.X / sw,
                r.Y / sh,
                (r.X + r.Width) / sw,
                (r.Y + r.Height) / sh);
        }
        return (texture, 0f, 0f, 1f, 1f);
    }
}
