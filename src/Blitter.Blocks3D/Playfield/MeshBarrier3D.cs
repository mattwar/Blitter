using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// A barrier based on a mesh.
/// </summary>
public class MeshBarrier3D<TVertex> : Barrier3D
    where TVertex : unmanaged, IPositionVertex3D
{
    private readonly Mesh<TVertex> _mesh;

    // Hot-path caches. Rebuilt when _mesh.Version changes so that
    // collision-time loops touch a tight Vector3[] (12 B/elem) rather
    // than dragging full vertices (often 32-56 B/elem) through cache.
    private Vector3[] _positions = Array.Empty<Vector3>();
    private Vector3[] _faceNormals = Array.Empty<Vector3>();
    private BoundingSphere _bounds;
    private int _cachedVersion = -1;

    /// <summary>
    /// Wraps <paramref name="mesh"/> as a barrier. The mesh's topology
    /// must be <see cref="Topology.TriangleList"/>; vertex positions
    /// are read in whatever space they're authored in, so pre-transform
    /// to world space if the barrier won't move.
    /// </summary>
    public MeshBarrier3D(Mesh<TVertex> mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.Topology != Topology.TriangleList)
            throw new ArgumentException(
                $"MeshBarrier3D requires {nameof(Topology)}.{nameof(Topology.TriangleList)}; got {mesh.Topology}.",
                nameof(mesh));
        _mesh = mesh;
    }

    /// <summary>
    /// The mesh used for both collision and (optionally) drawing.
    /// Override <see cref="Barrier3D.Draw"/> to render it.
    /// </summary>
    public Mesh<TVertex> Mesh => _mesh;

    /// <summary>Number of triangles in the mesh.</summary>
    public int TriangleCount
    {
        get
        {
            EnsureCache();
            return _positions.Length / 3;
        }
    }

    /// <summary>Broad-phase sphere enclosing every vertex.</summary>
    public BoundingSphere Bounds
    {
        get
        {
            EnsureCache();
            return _bounds;
        }
    }

    /// <summary>
    /// Per-triangle outward face normals, derived from each triangle's
    /// winding (<c>Cross(v1-v0, v2-v0)</c> normalised). Degenerate
    /// (zero-area) triangles yield <see cref="Vector3.Zero"/>.
    /// </summary>
    public ReadOnlySpan<Vector3> FaceNormals
    {
        get
        {
            EnsureCache();
            return _faceNormals;
        }
    }

    /// <inheritdoc/>
    public override bool Intersects(BoundingSphere sphere)
    {
        EnsureCache();
        if (sphere.IsEmpty || !_bounds.Intersects(sphere))
            return false;

        var r2 = sphere.Radius * sphere.Radius;
        var c = sphere.Center;
        var verts = _positions;
        for (int i = 0; i < verts.Length; i += 3)
        {
            var closest = Geometry3D.ClosestPointOnTriangle(c, verts[i], verts[i + 1], verts[i + 2]);
            if (Vector3.DistanceSquared(c, closest) <= r2)
                return true;
        }
        return false;
    }

    private void EnsureCache()
    {
        if (_cachedVersion == _mesh.Version)
            return;

        var verts = _mesh.Vertices;
        var indices = _mesh.Indices;

        int positionCount;
        if (indices.Length > 0)
        {
            if (indices.Length % 3 != 0)
                throw new InvalidOperationException("Mesh index count is not a multiple of 3.");
            positionCount = indices.Length;
        }
        else
        {
            if (verts.Length % 3 != 0)
                throw new InvalidOperationException("Mesh vertex count is not a multiple of 3.");
            positionCount = verts.Length;
        }

        if (_positions.Length != positionCount)
        {
            _positions = new Vector3[positionCount];
            _faceNormals = new Vector3[positionCount / 3];
        }

        if (indices.Length > 0)
        {
            for (int i = 0; i < indices.Length; i++)
                _positions[i] = verts[(int)indices[i]].Position;
        }
        else
        {
            for (int i = 0; i < verts.Length; i++)
                _positions[i] = verts[i].Position;
        }

        for (int i = 0, t = 0; i < _positions.Length; i += 3, t++)
        {
            var n = Vector3.Cross(_positions[i + 1] - _positions[i], _positions[i + 2] - _positions[i]);
            var lenSq = n.LengthSquared();
            _faceNormals[t] = lenSq > float.Epsilon ? n / MathF.Sqrt(lenSq) : Vector3.Zero;
        }

        _bounds = BoundingSphere.FromPoints(_positions);
        _cachedVersion = _mesh.Version;
    }
}
