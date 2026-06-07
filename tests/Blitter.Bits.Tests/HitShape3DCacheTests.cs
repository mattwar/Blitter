using Blitter.Bits;

namespace Blitter.Tests;

public class HitShape3DCacheTests
{
    [Fact]
    public void Default_IsSharedSingleton()
    {
        Assert.NotNull(HitShape3DCache.Default);
        Assert.Same(HitShape3DCache.Default, HitShape3DCache.Default);
    }

    [Fact]
    public void GetOrCreate_CachesPerMesh()
    {
        var cache = new HitShape3DCache();
        var mesh = Meshes.Cube(Color.White);

        var first = cache.GetOrCreateHitShape(mesh);
        var second = cache.GetOrCreateHitShape(mesh);
        Assert.Same(first, second);
    }

    [Fact]
    public void GetOrCreate_DistinctMeshes_GetDistinctShapes()
    {
        var cache = new HitShape3DCache();
        var a = Meshes.Cube(Color.White);
        var b = Meshes.Cube(Color.White);

        var shapeA = cache.GetOrCreateHitShape(a);
        var shapeB = cache.GetOrCreateHitShape(b);
        Assert.NotSame(shapeA, shapeB);
    }

    [Fact]
    public void GetOrCreate_CubeMesh_ProducesBoxShape()
    {
        var cache = new HitShape3DCache();
        var shape = cache.GetOrCreateHitShape(Meshes.Cube(Color.White));
        Assert.IsType<BoxHitShape3D>(shape);
    }

    [Fact]
    public void GetOrCreate_NullMesh_Throws()
    {
        var cache = new HitShape3DCache();
        Assert.Throws<ArgumentNullException>(() => cache.GetOrCreateHitShape((Mesh)null!));
    }
}
