using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

public class BoxBarrier3DTests
{
    [Fact]
    public void StoresCenterHalfExtentsAndRotation()
    {
        var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f);
        var box = new BoxBarrier3D(new Vector3(1f, 2f, 3f), new Vector3(2f, 3f, 4f), rot);

        Assert.Equal(new Vector3(1f, 2f, 3f), box.Center);
        Assert.Equal(new Vector3(2f, 3f, 4f), box.HalfExtents);
        Assert.Equal(rot, box.Rotation);
    }

    [Fact]
    public void NegativeHalfExtents_AreClampedToZero()
    {
        var box = new BoxBarrier3D(Vector3.Zero, new Vector3(-1f, 2f, -3f));
        Assert.Equal(new Vector3(0f, 2f, 0f), box.HalfExtents);
    }

    [Fact]
    public void DefaultRotation_IsIdentity()
    {
        var box = new BoxBarrier3D(Vector3.Zero, Vector3.One);
        Assert.Equal(Quaternion.Identity, box.Rotation);
    }

    [Fact]
    public void FromBoundingBox_UsesCenterAndExtents()
    {
        var bb = new BoundingBox(new Vector3(-1f, -2f, -3f), new Vector3(1f, 2f, 3f));
        var box = new BoxBarrier3D(bb);

        Assert.Equal(bb.Center, box.Center);
        Assert.Equal(bb.Extents, box.HalfExtents);
    }

    [Fact]
    public void HitShape_IsBoxPosedAtCenter()
    {
        var box = new BoxBarrier3D(new Vector3(5f, 0f, 0f), new Vector3(1f, 1f, 1f));
        var posed = box.HitShape;

        var shape = Assert.IsType<BoxHitShape3D>(posed.Shape);
        Assert.Equal(Vector3.Zero, shape.LocalCenter);
        Assert.Equal(new Vector3(1f, 1f, 1f), shape.LocalHalfExtents);
        Assert.Equal(new Vector3(5f, 0f, 0f), posed.Pose.Position);
    }
}
