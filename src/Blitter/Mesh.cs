using System.Runtime.InteropServices;

namespace Blitter;

/// <summary>
/// A base class for mesh data declared in CPU memory. 
/// </summary>
public abstract class Mesh
{
    /// <summary>
    /// Private so subtypes are limited to the generic <see cref="Mesh{TVertex}"/> implementation.
    /// </summary>
    private protected Mesh() { }

    /// <summary>
    /// The number of vertices in the mesh.
    /// </summary>
    public abstract int VertexCount { get; }

    /// <summary>
    /// The number of indices in the mesh. 
    /// If zero, the mesh is rendered as laid out in vertex order.
    /// </summary>
    public abstract int IndexCount { get; }

    /// <summary>
    /// Gets a span of the mesh's vertex data as raw bytes. 
    /// The renderer uses this to upload the vertex buffer to the GPU.
    /// </summary>
    /// <returns></returns>
    internal abstract ReadOnlySpan<byte> GetVertexBytes();

    /// <summary>
    /// Gets a span of the mesh's index buffer. 
    /// The renderer uses this to upload the index buffer to the GPU.
    /// </summary>
    public abstract ReadOnlySpan<uint> Indices { get; }

    /// <summary>
    /// The CLR type of this mesh's vertices.
    /// This is the type argument <c>TVertex</c> of the concrete <see cref="Mesh{TVertex}"/> subclass.
    /// </summary>
    public abstract Type VertexType { get; }

    /// <summary>
    /// How the mesh's vertices are grouped into rendered shapes (triangles, lines, points).
    /// </summary>
    public abstract Topology Topology { get; }

    /// <summary>
    /// Bumped each time the mesh's contents are replaced. 
    /// The renderer uses this to detect when its cached GPU vertex buffer needs to be re-uploaded.
    /// </summary>
    public int Version { get; private protected set; }

    /// <summary>
    /// Creates a <see cref="Mesh{TVertex}"/> with only vertices.
    /// </summary>
    public static Mesh<TVertex> Create<TVertex>(
        ReadOnlySpan<TVertex> vertices,
        Topology topology = Topology.TriangleList)
        where TVertex : unmanaged 
        =>
        new Mesh<TVertex>(vertices, ReadOnlySpan<uint>.Empty, topology);

    /// <summary>
    /// Creates a <see cref="Mesh{TVertex}"/> with vertices and indices
    /// to describe the order and reuse of vertices.
    /// </summary>
    public static Mesh<TVertex> Create<TVertex>(
        ReadOnlySpan<TVertex> vertices,
        ReadOnlySpan<uint> indices,
        Topology topology = Topology.TriangleList)
        where TVertex : unmanaged 
        =>
        new Mesh<TVertex>(vertices, indices, topology);
}

/// <summary>
/// A <see cref="Mesh"/> with strongly-typed vertex data.
/// The renderer uploads the mesh's data to the GPU as needed.
/// The mesh can be updated, but only one version will be uploaded per frame.
/// Similar to behavior of the <see cref="Bitmap"/> class.
/// </summary>
public class Mesh<TVertex> : Mesh
    where TVertex : unmanaged
{
    private TVertex[] _vertices;
    private int _vertexCount;

    private uint[] _indices;
    private int _indexCount;

    internal Mesh(ReadOnlySpan<TVertex> vertices)
        : this(vertices, ReadOnlySpan<uint>.Empty, Topology.TriangleList)
    {
    }

    internal Mesh(
        ReadOnlySpan<TVertex> vertices,
        ReadOnlySpan<uint> indices,
        Topology topology = Topology.TriangleList)
    {
        _vertices = vertices.Length == 0 ? Array.Empty<TVertex>() : new TVertex[vertices.Length];
        vertices.CopyTo(_vertices);
        _vertexCount = vertices.Length;

        _indices = indices.Length == 0 ? Array.Empty<uint>() : new uint[indices.Length];
        indices.CopyTo(_indices);
        _indexCount = indices.Length;

        Topology = topology;
        Version = 1;
    }

    // <inheritdoc/>
    public override int VertexCount => _vertexCount;

    // <inheritdoc/>
    public override int IndexCount => _indexCount;

    // <inheritdoc/>
    public override Topology Topology { get; }

    // <inheritdoc/>
    public override Type VertexType => typeof(TVertex);

    // <inheritdoc/>
    public ReadOnlySpan<TVertex> Vertices => _vertices.AsSpan(0, _vertexCount);

    // <inheritdoc/>
    internal override ReadOnlySpan<byte> GetVertexBytes() =>
        MemoryMarshal.AsBytes(_vertices.AsSpan(0, _vertexCount));

    // <inheritdoc/>
    public override ReadOnlySpan<uint> Indices =>
        _indices.AsSpan(0, _indexCount);

    /// <summary>
    /// Updates the vertex data.
    /// </summary>
    public void Update(ReadOnlySpan<TVertex> vertices)
    {
        EnsureVertexCapacity(vertices.Length);
        vertices.CopyTo(_vertices);
        _vertexCount = vertices.Length;
        unchecked { Version++; }
    }

    /// <summary>
    /// Updates the vertex and index data.
    /// </summary>
    public void Update(ReadOnlySpan<TVertex> vertices, ReadOnlySpan<uint> indices)
    {
        EnsureVertexCapacity(vertices.Length);
        vertices.CopyTo(_vertices);
        _vertexCount = vertices.Length;

        EnsureIndexCapacity(indices.Length);
        indices.CopyTo(_indices);
        _indexCount = indices.Length;

        unchecked { Version++; }
    }

    private void EnsureVertexCapacity(int count)
    {
        if (_vertices.Length < count)
            _vertices = new TVertex[count];
    }

    private void EnsureIndexCapacity(int count)
    {
        if (_indices.Length < count)
            _indices = new uint[count];
    }
}
