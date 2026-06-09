using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// A full unit cube: six flat faces, each textured through a
/// <see cref="VoxelTexture"/>. A face whose neighbor occludes it is
/// skipped during meshing. This is the default voxel shape.
/// </summary>
public sealed class CubeVoxelShape : VoxelShape
{
    /// <summary>A textureless cube; the default for <see cref="VoxelType.Shape"/>.</summary>
    public static readonly CubeVoxelShape Untextured = new((VoxelTexture?)null);

    /// <summary>
    /// The texture mapping for the six faces, or null for an untextured
    /// cube (the mesher uses a default material).
    /// </summary>
    public VoxelTexture? Texture { get; }

    /// <summary>
    /// How the cube's faces composite their texture alpha.
    /// <see cref="TransparencyMode.Opaque"/> (default) ignores alpha;
    /// <see cref="TransparencyMode.Cutout"/> discards texels below 0.5
    /// alpha for crisp holes (foliage); <see cref="TransparencyMode.Blend"/>
    /// alpha-blends for tinted glass. For the see-through modes, pair with
    /// <see cref="VoxelType.IsOpaque"/> set to false so neighbors keep the
    /// faces they share with this cube.
    /// </summary>
    public TransparencyMode Transparency { get; }

    /// <summary>
    /// Creates a cube textured by <paramref name="texture"/>. Pass a
    /// <paramref name="transparency"/> mode to draw it as a see-through
    /// cutout or alpha-blended surface. A single <see cref="Texture2D"/>
    /// also binds here (via the implicit conversion to
    /// <see cref="VoxelTexture"/>) for the all-faces-the-same case.
    /// </summary>
    public CubeVoxelShape(VoxelTexture? texture, TransparencyMode transparency = TransparencyMode.Opaque)
    {
        Texture = texture;
        Transparency = transparency;
    }

    /// <summary>
    /// Creates a cube with one texture on the top and bottom caps
    /// (<paramref name="topBottom"/>) and another shared by the four
    /// sides (<paramref name="sides"/>). The classic log / pillar layout.
    /// </summary>
    public CubeVoxelShape(
        Texture2D? topBottom,
        Texture2D? sides,
        TransparencyMode transparency = TransparencyMode.Opaque)
        : this(new TopSideBottomVoxelTexture(topBottom, sides, topBottom), transparency)
    {
    }

    /// <summary>
    /// Creates a cube with a distinct <paramref name="top"/>,
    /// <paramref name="sides"/>, and <paramref name="bottom"/> texture.
    /// The classic grass-block layout. Parameter order mirrors
    /// <see cref="TopSideBottomVoxelTexture"/> (top, sides, bottom).
    /// </summary>
    public CubeVoxelShape(
        Texture2D? top,
        Texture2D? sides,
        Texture2D? bottom,
        TransparencyMode transparency = TransparencyMode.Opaque)
        : this(new TopSideBottomVoxelTexture(top, sides, bottom), transparency)
    {
    }

    /// <summary>
    /// Creates a cube with a separately declared texture for each of the
    /// six faces, addressed in <see cref="VoxelFace"/> order
    /// (−X, +X, −Y, +Y, −Z, +Z).
    /// </summary>
    public CubeVoxelShape(
        Texture2D? negativeX,
        Texture2D? positiveX,
        Texture2D? negativeY,
        Texture2D? positiveY,
        Texture2D? negativeZ,
        Texture2D? positiveZ,
        TransparencyMode transparency = TransparencyMode.Opaque)
        : this(
            new SixFaceVoxelTexture(negativeX, positiveX, negativeY, positiveY, negativeZ, positiveZ),
            transparency)
    {
    }

    /// <inheritdoc/>
    public override bool FillsVoxel => true;

    private static readonly Vector3[] _faceNormals =
    {
        new(-1f, 0f, 0f), // -X
        new( 1f, 0f, 0f), // +X
        new( 0f,-1f, 0f), // -Y
        new( 0f, 1f, 0f), // +Y
        new( 0f, 0f,-1f), // -Z
        new( 0f, 0f, 1f), // +Z
    };

    // Per face: origin corner (unit-cell coords), two edge axes, and
    // a flag indicating whether the U axis follows E1 or E2.
    // origin + e1 + e2 + (cross e1 e2 = normal) gives an outward-CCW
    // quad. The UV swap is needed on +X and -Z because their E1 runs
    // vertically (+Y) while the texture's U is the horizontal axis.
    private static readonly (Vector3 Origin, Vector3 E1, Vector3 E2, bool UAlongE1)[] _faceCorners =
    {
        (new(0f, 0f, 0f), new(0f, 0f, 1f), new(0f, 1f, 0f), true ),  // -X: U=+Z, V=+Y
        (new(1f, 0f, 0f), new(0f, 1f, 0f), new(0f, 0f, 1f), false),  // +X: U=+Z, V=+Y
        (new(0f, 0f, 0f), new(1f, 0f, 0f), new(0f, 0f, 1f), true ),  // -Y
        (new(0f, 1f, 0f), new(0f, 0f, 1f), new(1f, 0f, 0f), true ),  // +Y
        (new(0f, 0f, 0f), new(0f, 1f, 0f), new(1f, 0f, 0f), false),  // -Z: U=+X, V=+Y
        (new(0f, 0f, 1f), new(1f, 0f, 0f), new(0f, 1f, 0f), true ),  // +Z: U=+X, V=+Y
    };

    /// <inheritdoc/>
    internal override void Build(in VoxelMeshContext context, IChunkMeshBuilder builder)
    {
        var cellSize = context.CellSize;
        // Same-type culling matters for the see-through modes: two
        // adjacent glass cubes shouldn't draw the doubled face between
        // them. For opaque cubes this is equivalent to IsNeighborOpaque.
        VoxelType ownType = context.Voxel;
        for (int face = 0; face < 6; face++)
        {
            if (context.IsNeighborOccluding((VoxelFace)face, ownType))
                continue;

            var (source, u0, v0, u1, v1) = ResolveUvRect(Texture?.GetFace((VoxelFace)face));
            EmitFace(builder, source, Transparency, context.X, context.Y, context.Z, cellSize, face, u0, v0, u1, v1);
        }
    }

    private static void EmitFace(
        IChunkMeshBuilder builder,
        Texture2D? sourceTexture,
        TransparencyMode transparency,
        int cellX, int cellY, int cellZ,
        Vector3 cellSize,
        int face,
        float u0, float v0, float u1, float v1)
    {
        var (origin, e1, e2, uAlongE1) = _faceCorners[face];
        var normal = _faceNormals[face];
        var cellOrigin = new Vector3(cellX, cellY, cellZ) * cellSize;

        var c0 = cellOrigin + origin * cellSize;
        var c1 = c0 + e1 * cellSize;
        var c2 = c1 + e2 * cellSize;
        var c3 = c0 + e2 * cellSize;

        // U follows the horizontal edge, V follows the other edge.
        // V=v0 is the texture's top, so the corner farthest along the
        // V edge gets v0 and the corner at the V edge origin gets v1.
        Vector2 uv0, uv1, uv2, uv3;
        if (uAlongE1)
        {
            uv0 = new Vector2(u0, v1);
            uv1 = new Vector2(u1, v1);
            uv2 = new Vector2(u1, v0);
            uv3 = new Vector2(u0, v0);
        }
        else
        {
            uv0 = new Vector2(u0, v1);
            uv1 = new Vector2(u0, v0);
            uv2 = new Vector2(u1, v0);
            uv3 = new Vector2(u1, v1);
        }

        var v0v = new LitTextureVertex3D(c0, normal, uv0);
        var v1v = new LitTextureVertex3D(c1, normal, uv1);
        var v2v = new LitTextureVertex3D(c2, normal, uv2);
        var v3v = new LitTextureVertex3D(c3, normal, uv3);
        builder.AddQuad(sourceTexture, transparency, in v0v, in v1v, in v2v, in v3v);
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
            // Half-texel inset: stops bilinear filtering at tile edges
            // from bleeding into neighbor tiles in a shared atlas.
            const float inset = 0.5f;
            return (src,
                (r.X + inset) / sw,
                (r.Y + inset) / sh,
                (r.X + r.Width - inset) / sw,
                (r.Y + r.Height - inset) / sh);
        }
        return (texture, 0f, 0f, 1f, 1f);
    }
}
