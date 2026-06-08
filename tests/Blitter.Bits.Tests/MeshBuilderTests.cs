using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

public class MeshBuilderTests
{
    private static Vertex3D V(float x, float y, float z) => new(x, y, z);

    [Fact]
    public void NewBuilder_IsEmpty_AndHasNoMesh()
    {
        var b = new MeshBuilder<Vertex3D>();
        Assert.True(b.IsEmpty);
        Assert.Equal(0, b.VertexCount);
        Assert.Equal(0, b.IndexCount);
        Assert.Null(b.Mesh);
    }

    [Fact]
    public void AddVertex_ReturnsRunningIndex()
    {
        var b = new MeshBuilder<Vertex3D>();
        Assert.Equal(0u, b.AddVertex(V(0, 0, 0)));
        Assert.Equal(1u, b.AddVertex(V(1, 0, 0)));
        Assert.Equal(2, b.VertexCount);
        Assert.False(b.IsEmpty);
    }

    [Fact]
    public void AddTriangle_AppendsThreeVerticesAndIndices()
    {
        var b = new MeshBuilder<Vertex3D>();
        b.AddTriangle(V(0, 0, 0), V(1, 0, 0), V(0, 1, 0));
        Assert.Equal(3, b.VertexCount);
        Assert.Equal(3, b.IndexCount);
    }

    [Fact]
    public void AddQuad_AppendsFourVerticesAndSixIndices()
    {
        var b = new MeshBuilder<Vertex3D>();
        b.AddQuad(V(0, 0, 0), V(1, 0, 0), V(1, 1, 0), V(0, 1, 0));
        Assert.Equal(4, b.VertexCount);
        Assert.Equal(6, b.IndexCount);
    }

    [Fact]
    public void Flush_CreatesMeshWithBufferedData()
    {
        var b = new MeshBuilder<Vertex3D>();
        b.AddTriangle(V(0, 0, 0), V(1, 0, 0), V(0, 1, 0));
        var mesh = b.Flush();

        Assert.NotNull(mesh);
        Assert.Same(mesh, b.Mesh);
        Assert.Equal(3, mesh.Vertices.Length);
        Assert.Equal(3, mesh.Indices.Length);
    }

    [Fact]
    public void Flush_CalledTwice_IsIdempotentAndReusesMesh()
    {
        var b = new MeshBuilder<Vertex3D>();
        b.AddTriangle(V(0, 0, 0), V(1, 0, 0), V(0, 1, 0));
        var first = b.Flush();
        var second = b.Flush();
        Assert.Same(first, second);
    }

    [Fact]
    public void Clear_DropsBufferedData_ButKeepsMeshAlive()
    {
        var b = new MeshBuilder<Vertex3D>();
        b.AddTriangle(V(0, 0, 0), V(1, 0, 0), V(0, 1, 0));
        var mesh = b.Flush();

        b.Clear();
        Assert.True(b.IsEmpty);
        Assert.Equal(0, b.VertexCount);
        Assert.Equal(0, b.IndexCount);
        // The owned mesh survives a Clear (GPU buffers reused).
        Assert.Same(mesh, b.Mesh);
    }

    [Fact]
    public void Rebuild_ReusesSameMeshInstance()
    {
        var b = new MeshBuilder<Vertex3D>();
        b.AddTriangle(V(0, 0, 0), V(1, 0, 0), V(0, 1, 0));
        var mesh = b.Flush();

        b.Clear();
        b.AddQuad(V(0, 0, 0), V(1, 0, 0), V(1, 1, 0), V(0, 1, 0));
        var rebuilt = b.Flush();

        Assert.Same(mesh, rebuilt);
        Assert.Equal(4, rebuilt.Vertices.Length);
        Assert.Equal(6, rebuilt.Indices.Length);
    }

    [Fact]
    public void DefaultTopology_IsTriangleList()
    {
        var b = new MeshBuilder<Vertex3D>();
        b.AddTriangle(V(0, 0, 0), V(1, 0, 0), V(0, 1, 0));
        Assert.Equal(Topology.TriangleList, b.Flush().Topology);
    }
}
