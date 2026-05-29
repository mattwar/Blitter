using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// <see cref="Visual3D"/> for one voxel chunk. 
/// Re-meshes lazily when the underlying <see cref="VoxelChunkGrid"/>'s world reports
/// changes within (or adjacent to) the chunk. 
/// Geometry is bucketed by source <see cref="Texture2D"/>: 
/// every <see cref="TextureRegion2D"/> pointing at the same underlying texture collapses into a single mesh and
/// material so chunks fully mapped to one source texture draw in one call.
/// </summary>
public sealed class VoxelChunkVisual3D : Visual3D, IVoxelMeshSink
{
    private readonly VoxelChunkGrid _grid;
    private readonly VoxelHitShape3D _hitShape;
    private readonly BoundingSphere _boundary;
    // One builder + material per source texture. The untextured group
    // (cells whose VoxelType.Texture is null) is held separately so
    // the dictionary key stays non-nullable.
    private readonly Dictionary<Texture2D, TextureGroup> _groups = new(ReferenceEqualityComparer.Instance);
    private TextureGroup? _untextured;
    private bool _dirty = true;

    // Sticky cache: the mesher walks all six faces of a cell in a row,
    // so most EmitQuad calls hit the same source texture. Skips the
    // dictionary lookup for the common run.
    private Texture2D? _lastSource;
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
        grid.World.VoxelsChanged += OnVoxelsChanged;
    }

    public VoxelChunkGrid Grid => _grid;

    public override BoundingSphere Boundary => _boundary;

    public override HitShape3D HitShape => _hitShape;

    public override void Draw(Renderer3D renderer, in Pose3D pose, Color tint, TimeSpan elapsed)
    {
        if (_dirty)
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
    }

    /// <summary>Forces a re-mesh on next draw.</summary>
    public void Invalidate() => _dirty = true;

    private void OnVoxelsChanged(object? sender, VoxelChangeEventArgs e)
    {
        // Expand by one cell on each axis: a neighbor edit can flip a
        // face inside this chunk from hidden to visible.
        int lx0 = e.MinX - _grid.OriginCellX - 1;
        int lx1 = e.MaxX - _grid.OriginCellX + 1;
        int ly0 = e.MinY - _grid.OriginCellY - 1;
        int ly1 = e.MaxY - _grid.OriginCellY + 1;
        int lz0 = e.MinZ - _grid.OriginCellZ - 1;
        int lz1 = e.MaxZ - _grid.OriginCellZ + 1;
        if (lx1 < 0 || lx0 >= _grid.CellsX
            || ly1 < 0 || ly0 >= _grid.CellsY
            || lz1 < 0 || lz0 >= _grid.CellsZ)
            return;
        _dirty = true;
    }

    private void Rebuild()
    {
        _untextured?.Builder.Clear();
        foreach (var group in _groups.Values)
            group.Builder.Clear();

        // Empty groups are kept around so their mesh + GPU buffers can
        // be reused next rebuild when the same source texture reappears.
        _lastSource = null;
        _lastBuilder = null;
        VoxelMesher.Build(_grid, this);
        _dirty = false;
    }

    void IVoxelMeshSink.EmitQuad(
        Texture2D? sourceTexture,
        in LitTextureVertex3D v0,
        in LitTextureVertex3D v1,
        in LitTextureVertex3D v2,
        in LitTextureVertex3D v3)
    {
        var builder = _lastBuilder;
        if (builder is null || !ReferenceEquals(_lastSource, sourceTexture))
        {
            builder = GetOrCreateGroup(sourceTexture).Builder;
            _lastSource = sourceTexture;
            _lastBuilder = builder;
        }
        builder.AddQuad(in v0, in v1, in v2, in v3);
    }

    private TextureGroup GetOrCreateGroup(Texture2D? sourceTexture)
    {
        if (sourceTexture is null)
            return _untextured ??= new TextureGroup(null);
        if (!_groups.TryGetValue(sourceTexture, out var group))
        {
            group = new TextureGroup(sourceTexture);
            _groups.Add(sourceTexture, group);
        }
        return group;
    }

    private sealed record TextureGroup(LitTextureMaterial Material, MeshBuilder<LitTextureVertex3D> Builder)
    {
        public TextureGroup(Texture2D? sourceTexture)
            : this(
                sourceTexture is null
                    ? LitTextureMaterial.Default
                    : new LitTextureMaterial { DiffuseTexture = sourceTexture },
                new MeshBuilder<LitTextureVertex3D>())
        {
        }
    }
}
