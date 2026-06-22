using Blitter.Bits;

namespace Blitter.Tests;

public class MeshFit3DTests
{
    private const float Eps = 1e-3f;

    [Fact]
    public void EmptyMesh_ReturnsNone()
    {
        var mesh = Mesh.Create<Vertex3D>(ReadOnlySpan<Vertex3D>.Empty);
        var shape = mesh.ComputeAutoHitShape3D();
        Assert.Same(HitShape3D.None, shape);
    }

    [Fact]
    public void Cube_FitsBox()
    {
        var cube = Meshes.Cube(Color.White);
        var shape = cube.ComputeAutoHitShape3D();
        // A near-cubic AABB: box beats sphere/capsule/cylinder on volume.
        var box = Assert.IsType<BoxHitShape3D>(shape);
        Assert.Equal(0f, box.LocalCenter.X, Eps);
        Assert.Equal(0f, box.LocalCenter.Y, Eps);
        Assert.Equal(0f, box.LocalCenter.Z, Eps);
    }

    [Fact]
    public void Sphere_FitsSphere()
    {
        var sphere = Meshes.Sphere(Color.White);
        var shape = sphere.ComputeAutoHitShape3D();
        // A round mesh: the bounding sphere is tighter than its AABB.
        Assert.IsType<SphereHitShape3D>(shape);
    }

    [Fact]
    public void Fit_EnclosesEveryVertex()
    {
        var cube = Meshes.Cube(Color.White);
        var box = Assert.IsType<BoxHitShape3D>(cube.ComputeAutoHitShape3D());

        var min = box.LocalCenter - box.LocalHalfExtents;
        var max = box.LocalCenter + box.LocalHalfExtents;
        foreach (var v in cube.Vertices)
        {
            var p = v.Position;
            Assert.True(p.X >= min.X - Eps && p.X <= max.X + Eps);
            Assert.True(p.Y >= min.Y - Eps && p.Y <= max.Y + Eps);
            Assert.True(p.Z >= min.Z - Eps && p.Z <= max.Z + Eps);
        }
    }

    [Fact]
    public void Null_Throws()
    {
        Mesh? mesh = null;
        Assert.Throws<ArgumentNullException>(() => mesh!.ComputeAutoHitShape3D());
    }
}
