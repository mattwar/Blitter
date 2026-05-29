using System.Runtime.InteropServices;

namespace Blitter.Bits;

/// <summary>
/// Reusable CPU-side accumulator for a <see cref="Mesh{TVertex}"/>.
/// Append vertices and indices across a frame, call
/// <see cref="Flush"/> to push them into the owned mesh, then
/// <see cref="Clear"/> before the next rebuild. The same <see cref="Mesh"/>
/// instance is reused across frames so the renderer's GPU buffers grow
/// monotonically instead of being reallocated.
/// </summary>
public sealed class MeshBuilder<TVertex>
    where TVertex : unmanaged
{
    private readonly List<TVertex> _vertices = new();
    private readonly List<uint> _indices = new();
    private readonly Topology _topology;
    private Mesh<TVertex>? _mesh;
    private bool _flushed;

    public MeshBuilder(Topology topology = Topology.TriangleList)
    {
        _topology = topology;
    }

    /// <summary>Number of vertices currently buffered.</summary>
    public int VertexCount => _vertices.Count;

    /// <summary>Number of indices currently buffered.</summary>
    public int IndexCount => _indices.Count;

    /// <summary>True when no vertices have been appended since the last <see cref="Clear"/>.</summary>
    public bool IsEmpty => _vertices.Count == 0;

    /// <summary>
    /// Owned <see cref="Mesh"/>. Created on first <see cref="Flush"/>;
    /// <c>null</c> before any flush.
    /// </summary>
    public Mesh<TVertex>? Mesh => _mesh;

    /// <summary>Drops all buffered vertices and indices. Keeps the mesh and its GPU buffers alive.</summary>
    public void Clear()
    {
        _vertices.Clear();
        _indices.Clear();
        _flushed = false;
    }

    /// <summary>Appends one vertex. Returns its index.</summary>
    public uint AddVertex(in TVertex v)
    {
        uint i = (uint)_vertices.Count;
        _vertices.Add(v);
        return i;
    }

    /// <summary>Appends one index.</summary>
    public void AddIndex(uint index) => _indices.Add(index);

    /// <summary>
    /// Appends three vertices and the three indices that form one
    /// triangle in the order given. Use for unshared-vertex triangle
    /// soups.
    /// </summary>
    public void AddTriangle(in TVertex a, in TVertex b, in TVertex c)
    {
        uint i = (uint)_vertices.Count;
        _vertices.Add(a);
        _vertices.Add(b);
        _vertices.Add(c);
        _indices.Add(i);
        _indices.Add(i + 1);
        _indices.Add(i + 2);
    }

    /// <summary>
    /// Appends four vertices and the six indices for two triangles
    /// (<c>a,b,c</c> and <c>a,c,d</c>) forming a quad. <paramref name="a"/>..
    /// <paramref name="d"/> should wind CCW when viewed from the outward face.
    /// </summary>
    public void AddQuad(in TVertex a, in TVertex b, in TVertex c, in TVertex d)
    {
        uint i = (uint)_vertices.Count;
        _vertices.Add(a);
        _vertices.Add(b);
        _vertices.Add(c);
        _vertices.Add(d);
        _indices.Add(i);
        _indices.Add(i + 1);
        _indices.Add(i + 2);
        _indices.Add(i);
        _indices.Add(i + 2);
        _indices.Add(i + 3);
    }

    /// <summary>
    /// Pushes the accumulated spans into the owned <see cref="Mesh"/>,
    /// creating it on first call. Safe to call multiple times between
    /// <see cref="Clear"/>s; subsequent calls are no-ops.
    /// </summary>
    public Mesh<TVertex> Flush()
    {
        if (_mesh is not null && _flushed)
            return _mesh;

        var vSpan = CollectionsMarshal.AsSpan(_vertices);
        var iSpan = CollectionsMarshal.AsSpan(_indices);
        if (_mesh is null)
            _mesh = Blitter.Mesh.Create<TVertex>(vSpan, iSpan, _topology);
        else
            _mesh.Update(vSpan, iSpan);
        _flushed = true;
        return _mesh;
    }
}
