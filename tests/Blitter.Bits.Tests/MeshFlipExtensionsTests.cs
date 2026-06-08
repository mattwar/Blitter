using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

public class MeshFlipExtensionsTests
{
    private static Mesh<Vertex3D> IndexedTriangle()
    {
        Span<Vertex3D> verts =
        [
            new Vertex3D(0f, 0f, 0f),
            new Vertex3D(1f, 0f, 0f),
            new Vertex3D(0f, 1f, 0f),
        ];
        Span<uint> indices = [0u, 1u, 2u];
        return Mesh.Create<Vertex3D>(verts, indices);
    }

    private static Mesh<Vertex3D> UnindexedTriangle()
    {
        Span<Vertex3D> verts =
        [
            new Vertex3D(0f, 0f, 0f),
            new Vertex3D(1f, 0f, 0f),
            new Vertex3D(0f, 1f, 0f),
        ];
        return Mesh.Create<Vertex3D>(verts);
    }

    [Fact]
    public void FlipWinding_Indexed_SwapsLastTwoIndices()
    {
        var flipped = IndexedTriangle().FlipWinding();
        var idx = flipped.Indices;
        Assert.Equal(3, idx.Length);
        Assert.Equal(0u, idx[0]);
        Assert.Equal(2u, idx[1]);
        Assert.Equal(1u, idx[2]);
    }

    [Fact]
    public void FlipWinding_Indexed_KeepsVerticesUntouched()
    {
        var src = IndexedTriangle();
        var flipped = src.FlipWinding();
        Assert.Equal(src.Vertices.Length, flipped.Vertices.Length);
        for (int i = 0; i < src.Vertices.Length; i++)
            Assert.Equal(src.Vertices[i].Position, flipped.Vertices[i].Position);
    }

    [Fact]
    public void FlipWinding_Unindexed_SwapsLastTwoVertices()
    {
        var flipped = UnindexedTriangle().FlipWinding();
        var v = flipped.Vertices;
        Assert.Equal(3, v.Length);
        Assert.Equal(new Vector3(0f, 0f, 0f), v[0].Position);
        Assert.Equal(new Vector3(0f, 1f, 0f), v[1].Position);
        Assert.Equal(new Vector3(1f, 0f, 0f), v[2].Position);
    }

    [Fact]
    public void FlipWinding_Null_Throws()
    {
        Mesh<Vertex3D>? mesh = null;
        Assert.Throws<ArgumentNullException>(() => mesh!.FlipWinding());
    }

    [Fact]
    public void FlipWinding_NonTriangleList_Throws()
    {
        Span<Vertex3D> verts =
        [
            new Vertex3D(0f, 0f, 0f),
            new Vertex3D(1f, 0f, 0f),
            new Vertex3D(0f, 1f, 0f),
        ];
        var strip = Mesh.Create<Vertex3D>(verts, Topology.TriangleStrip);
        Assert.Throws<InvalidOperationException>(() => strip.FlipWinding());
    }

    [Fact]
    public void FlipNormals_Lit_NegatesEveryNormal()
    {
        Span<LitVertex3D> verts =
        [
            new LitVertex3D(new Vector3(0, 0, 0), new Vector3(0, 1, 0), Color.White),
            new LitVertex3D(new Vector3(1, 0, 0), new Vector3(1, 0, 0), Color.White),
            new LitVertex3D(new Vector3(0, 1, 0), new Vector3(0, 0, 1), Color.White),
        ];
        var mesh = Mesh.Create<LitVertex3D>(verts);

        var flipped = mesh.FlipNormals();
        for (int i = 0; i < mesh.Vertices.Length; i++)
        {
            Assert.Equal(-mesh.Vertices[i].Normal, flipped.Vertices[i].Normal);
            // Positions are preserved.
            Assert.Equal(mesh.Vertices[i].Position, flipped.Vertices[i].Position);
        }
    }

    [Fact]
    public void FlipNormals_Null_Throws()
    {
        Mesh<LitVertex3D>? mesh = null;
        Assert.Throws<ArgumentNullException>(() => mesh!.FlipNormals());
    }
}
