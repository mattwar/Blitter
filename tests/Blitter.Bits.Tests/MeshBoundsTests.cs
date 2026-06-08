using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

public class MeshBoundsTests
{
    private const float Eps = 1e-4f;

    // A unit-ish tetrahedron spanning [0,2] on each axis.
    private static Mesh<Vertex3D> SampleMesh()
    {
        Span<Vertex3D> verts =
        [
            new Vertex3D(0f, 0f, 0f),
            new Vertex3D(2f, 0f, 0f),
            new Vertex3D(0f, 2f, 0f),
            new Vertex3D(0f, 0f, 2f),
        ];
        return Mesh.Create<Vertex3D>(verts);
    }

    [Fact]
    public void ComputeBoundingBox_Generic_EnclosesAllVertices()
    {
        var box = SampleMesh().ComputeBoundingBox();
        Assert.Equal(new Vector3(0f, 0f, 0f), box.Min);
        Assert.Equal(new Vector3(2f, 2f, 2f), box.Max);
    }

    [Fact]
    public void ComputeCenter_IsBoxCenter()
    {
        var center = SampleMesh().ComputeCenter();
        Assert.Equal(new Vector3(1f, 1f, 1f), center);
    }

    [Fact]
    public void ComputeBoundingSphere_ContainsAllVertices()
    {
        var mesh = SampleMesh();
        var sphere = mesh.ComputeBoundingSphere();
        foreach (var v in mesh.Vertices)
        {
            float d = Vector3.Distance(sphere.Center, v.Position);
            Assert.True(d <= sphere.Radius + Eps,
                $"Vertex {v.Position} lies outside the bounding sphere.");
        }
    }

    [Fact]
    public void ComputeBoundingBox_NonGeneric_MatchesGenericForKnownLayout()
    {
        Mesh mesh = SampleMesh();
        var box = mesh.ComputeBoundingBox();
        Assert.Equal(new Vector3(0f, 0f, 0f), box.Min);
        Assert.Equal(new Vector3(2f, 2f, 2f), box.Max);
    }

    [Fact]
    public void ComputeBoundingBox_Null_Throws()
    {
        Mesh<Vertex3D>? mesh = null;
        Assert.Throws<ArgumentNullException>(() => mesh!.ComputeBoundingBox());
    }

    [Fact]
    public void ComputeBoundingBox_OfStockCube_IsSymmetric()
    {
        var cube = Meshes.Cube(Color.White);
        var box = cube.ComputeBoundingBox();
        // A stock cube is centered on the origin.
        Assert.Equal(0f, box.Center.X, Eps);
        Assert.Equal(0f, box.Center.Y, Eps);
        Assert.Equal(0f, box.Center.Z, Eps);
        Assert.True(box.Size.X > 0f);
        Assert.True(box.Size.Y > 0f);
        Assert.True(box.Size.Z > 0f);
    }
}
