using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

public class SphereBarrier3DTests
{
    [Fact]
    public void StoresCenterAndRadius()
    {
        var sphere = new SphereBarrier3D(new Vector3(1f, 2f, 3f), 4f);
        Assert.Equal(new Vector3(1f, 2f, 3f), sphere.Center);
        Assert.Equal(4f, sphere.Radius);
    }

    [Fact]
    public void ComponentConstructor_BuildsCenter()
    {
        var sphere = new SphereBarrier3D(1f, 2f, 3f, 5f);
        Assert.Equal(new Vector3(1f, 2f, 3f), sphere.Center);
        Assert.Equal(5f, sphere.Radius);
    }

    [Fact]
    public void NegativeRadius_IsClampedToZero()
    {
        var sphere = new SphereBarrier3D(Vector3.Zero, -3f);
        Assert.Equal(0f, sphere.Radius);
    }

    [Fact]
    public void HitShape_IsSpherePosedAtCenter()
    {
        var sphere = new SphereBarrier3D(new Vector3(0f, 10f, 0f), 2f);
        var posed = sphere.HitShape;

        var shape = Assert.IsType<SphereHitShape3D>(posed.Shape);
        Assert.Equal(Vector3.Zero, shape.LocalCenter);
        Assert.Equal(2f, shape.LocalRadius);
        Assert.Equal(new Vector3(0f, 10f, 0f), posed.Pose.Position);
    }
}
