using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// <see cref="Visual3D"/> for one voxel chunk. 
/// Re-meshes lazily when its <see cref="VoxelChunkGrid.Version"/> changes,
/// checked on <see cref="Draw"/>. The version is bumped by the owning
/// <see cref="VoxelChunkSource3D"/> when the world reports a voxel change
/// this chunk reads, so the long-lived world never holds a reference to
/// the visual.
/// Geometry is bucketed by source <see cref="Texture2D"/>: 
/// every <see cref="TextureRegion2D"/> pointing at the same underlying texture collapses into a single mesh and
/// material so chunks fully mapped to one source texture draw in one call.
/// </summary>
internal sealed class VoxelChunkVisual3D : Visual3D, IChunkMeshBuilder
{
    private readonly VoxelChunkGrid _grid;
    private readonly VoxelHitShape3D _hitShape;
    private readonly BoundingSphere _boundary;
    // Grid version the current mesh was built against; compared on Draw
    // to decide when to re-mesh.
    private int _builtVersion;
    private bool _built;
    // One builder + material per source texture. The untextured group
    // (cells whose VoxelType.Texture is null) is held separately so
    // the dictionary key stays non-nullable.
    private readonly Dictionary<Texture2D, TextureGroup> _groups = new(ReferenceEqualityComparer.Instance);
    // Parallel bucket for alpha-cutout surfaces (foliage and the like):
    // same source texture an opaque group would use, but a cutout
    // material so the shader discards see-through texels.
    private readonly Dictionary<Texture2D, TextureGroup> _cutoutGroups = new(ReferenceEqualityComparer.Instance);
    private TextureGroup? _untextured;

    // Sticky cache: the mesher walks all six faces of a cell in a row,
    // so most AddQuad calls hit the same source texture. Skips the
    // dictionary lookup for the common run.
    private Texture2D? _lastSource;
    private bool _lastCutout;
    private MeshBuilder<LitTextureVertex3D>? _lastBuilder;

    public VoxelChunkVisual3D(VoxelChunkGrid grid)
        : this(grid, new VoxelHitShape3D(grid))
    {
    }

    /// <summary>
    /// Builds a visual that shares <paramref name="hitShape"/> with the
    /// chunk's barrier so collision and rendering see the same cells.
    /// </summary>
    public VoxelChunkVisual3D(VoxelChunkGrid grid, VoxelHitShape3D hitShape)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(hitShape);
        _grid = grid;
        _hitShape = hitShape;
        var size = new Vector3(grid.CellsX, grid.CellsY, grid.CellsZ) * grid.CellSize;
        _boundary = new BoundingSphere(size * 0.5f, size.Length() * 0.5f);
    }

    public VoxelChunkGrid Grid => _grid;

    public override BoundingSphere Boundary => _boundary;

    public override HitShape3D HitShape => _hitShape;

    public override void Draw(Renderer3D renderer, in Pose3D pose, Color tint, TimeSpan elapsed)
    {
        if (NeedsRebuild())
            Rebuild();

        var transform = pose.ToMatrix();
        if (_untextured is { } u && !u.Builder.IsEmpty)
            renderer.DrawMesh(u.Builder.Flush(), u.Material, transform);
        foreach (var group in _groups.Values)
        {
            if (group.Builder.IsEmpty)
                continue;
            renderer.DrawMesh(group.Builder.Flush(), group.Material, transform);
        }
        // Cutout groups draw after the opaque ones. They write depth and
        // discard see-through texels, so order against opaque geometry
        // doesn't matter, but drawing opaque first helps early-Z.
        foreach (var group in _cutoutGroups.Values)
        {
            if (group.Builder.IsEmpty)
                continue;
            renderer.DrawMesh(group.Builder.Flush(), group.Material, transform);
        }
    }

    /// <summary>Forces a re-mesh on next draw.</summary>
    public void Invalidate() => _built = false;

    /// <summary>
    /// Resets this visual so its owning chunk can be recycled onto a new
    /// coord. Clears every texture group's buffered geometry but keeps the
    /// <see cref="MeshBuilder{TVertex}"/> instances (and their grown GPU
    /// buffers) alive for reuse, and marks the mesh unbuilt so it re-meshes
    /// the recycled grid's data on the next draw.
    /// </summary>
    internal void ResetForReuse()
    {
        _untextured?.Builder.Clear();
        foreach (var group in _groups.Values)
            group.Builder.Clear();
        foreach (var group in _cutoutGroups.Values)
            group.Builder.Clear();
        _lastSource = null;
        _lastCutout = false;
        _lastBuilder = null;
        _builtVersion = 0;
        _built = false;
    }

    // internal for tests: true when the mesh is stale relative to the
    // current grid version.
    internal bool NeedsRebuild() => !_built || _grid.Version != _builtVersion;

    internal void Rebuild()
    {
        _untextured?.Builder.Clear();
        foreach (var group in _groups.Values)
            group.Builder.Clear();
        foreach (var group in _cutoutGroups.Values)
            group.Builder.Clear();

        // Empty groups are kept around so their mesh + GPU buffers can
        // be reused next rebuild when the same source texture reappears.
        _lastSource = null;
        _lastCutout = false;
        _lastBuilder = null;
        VoxelMesher.Build(_grid, this);
        // Snapshot the version the mesh was built against.
        _builtVersion = _grid.Version;
        _built = true;
    }

    void IChunkMeshBuilder.AddQuad(
        Texture2D? sourceTexture,
        bool alphaCutout,
        in LitTextureVertex3D v0,
        in LitTextureVertex3D v1,
        in LitTextureVertex3D v2,
        in LitTextureVertex3D v3)
    {
        ResolveBuilder(sourceTexture, alphaCutout).AddQuad(in v0, in v1, in v2, in v3);
    }

    void IChunkMeshBuilder.AddTriangle(
        Texture2D? sourceTexture,
        bool alphaCutout,
        in LitTextureVertex3D v0,
        in LitTextureVertex3D v1,
        in LitTextureVertex3D v2)
    {
        ResolveBuilder(sourceTexture, alphaCutout).AddTriangle(in v0, in v1, in v2);
    }

    private MeshBuilder<LitTextureVertex3D> ResolveBuilder(Texture2D? sourceTexture, bool alphaCutout)
    {
        var builder = _lastBuilder;
        if (builder is null || !ReferenceEquals(_lastSource, sourceTexture) || _lastCutout != alphaCutout)
        {
            builder = GetOrCreateGroup(sourceTexture, alphaCutout).Builder;
            _lastSource = sourceTexture;
            _lastCutout = alphaCutout;
            _lastBuilder = builder;
        }
        return builder;
    }

    private TextureGroup GetOrCreateGroup(Texture2D? sourceTexture, bool alphaCutout)
    {
        // Untextured geometry can't be cutout (there's no alpha to test),
        // so it always lands in the single opaque untextured group.
        if (sourceTexture is null)
            return _untextured ??= new TextureGroup(null, alphaCutout: false);
        var dict = alphaCutout ? _cutoutGroups : _groups;
        if (!dict.TryGetValue(sourceTexture, out var group))
        {
            group = new TextureGroup(sourceTexture, alphaCutout);
            dict.Add(sourceTexture, group);
        }
        return group;
    }

    private sealed record TextureGroup(LitTextureMaterial Material, MeshBuilder<LitTextureVertex3D> Builder)
    {
        public TextureGroup(Texture2D? sourceTexture, bool alphaCutout)
            : this(
                sourceTexture is null
                    ? LitTextureMaterial.Default
                    : new LitTextureMaterial { DiffuseTexture = sourceTexture, AlphaCutout = alphaCutout },
                new MeshBuilder<LitTextureVertex3D>())
        {
        }
    }
}
