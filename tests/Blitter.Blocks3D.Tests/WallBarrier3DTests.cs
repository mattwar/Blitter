using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

public class WallBarrier3DTests
{
    private const float Eps = 1e-5f;

    private static void AssertOrthonormal(WallBarrier3D wall)
    {
        Assert.Equal(1f, wall.Normal.Length(), 5);
        Assert.Equal(1f, wall.Tangent.Length(), 5);
        Assert.Equal(1f, wall.Bitangent.Length(), 5);
        Assert.Equal(0f, Vector3.Dot(wall.Normal, wall.Tangent), 5);
        Assert.Equal(0f, Vector3.Dot(wall.Normal, wall.Bitangent), 5);
        Assert.Equal(0f, Vector3.Dot(wall.Tangent, wall.Bitangent), 5);
    }

    [Fact]
    public void Floor_NormalPointsUp()
    {
        var floor = WallBarrier3D.Floor(Vector3.Zero, new Vector2(5f, 5f));
        Assert.Equal(new Vector3(0f, 1f, 0f), floor.Normal);
        AssertOrthonormal(floor);
    }

    [Fact]
    public void Ceiling_NormalPointsDown()
    {
        var ceiling = WallBarrier3D.Ceiling(new Vector3(0f, 10f, 0f), new Vector2(5f, 5f));
        Assert.Equal(new Vector3(0f, -1f, 0f), ceiling.Normal);
        AssertOrthonormal(ceiling);
    }

    [Fact]
    public void Normal_IsNormalized()
    {
        var wall = new WallBarrier3D(
            Vector3.Zero, new Vector3(0f, 5f, 0f), Vector3.UnitX, new Vector2(1f, 1f));
        Assert.Equal(new Vector3(0f, 1f, 0f), wall.Normal);
    }

    [Fact]
    public void DegenerateNormal_FallsBackToUnitY()
    {
        var wall = new WallBarrier3D(
            Vector3.Zero, Vector3.Zero, Vector3.UnitX, new Vector2(1f, 1f));
        Assert.Equal(Vector3.UnitY, wall.Normal);
    }

    [Fact]
    public void TangentHint_IsProjectedIntoPlane()
    {
        // Hint has a component along the normal; it must be removed.
        var wall = new WallBarrier3D(
            Vector3.Zero,
            Vector3.UnitY,
            new Vector3(1f, 3f, 0f), // Y component is parallel to normal
            new Vector2(1f, 1f));

        Assert.Equal(0f, Vector3.Dot(wall.Normal, wall.Tangent), 5);
        Assert.Equal(new Vector3(1f, 0f, 0f), wall.Tangent);
    }

    [Fact]
    public void DegenerateTangentHint_PicksAPerpendicular()
    {
        // Hint parallel to normal => must synthesize a perpendicular.
        var wall = new WallBarrier3D(
            Vector3.Zero, Vector3.UnitY, Vector3.UnitY, new Vector2(1f, 1f));
        AssertOrthonormal(wall);
    }

    [Fact]
    public void NegativeHalfExtents_AreClampedToZero()
    {
        var wall = new WallBarrier3D(
            Vector3.Zero, Vector3.UnitY, Vector3.UnitX, new Vector2(-2f, -3f));
        Assert.Equal(new Vector2(0f, 0f), wall.HalfExtents);
    }

    [Fact]
    public void Bitangent_IsCrossOfNormalAndTangent()
    {
        var wall = new WallBarrier3D(
            Vector3.Zero, Vector3.UnitY, Vector3.UnitX, new Vector2(1f, 1f));
        var expected = Vector3.Cross(wall.Normal, wall.Tangent);
        Assert.Equal(expected.X, wall.Bitangent.X, 5);
        Assert.Equal(expected.Y, wall.Bitangent.Y, 5);
        Assert.Equal(expected.Z, wall.Bitangent.Z, 5);
    }

    [Fact]
    public void OneSided_DefaultsFalse_AndFactoryHonorsFlag()
    {
        var open = WallBarrier3D.Floor(Vector3.Zero, new Vector2(1f, 1f));
        var oneWay = WallBarrier3D.Floor(Vector3.Zero, new Vector2(1f, 1f), oneSided: true);
        Assert.False(open.OneSided);
        Assert.True(oneWay.OneSided);
    }

    [Fact]
    public void Vertical_HasVerticalBitangentAndHorizontalNormal()
    {
        var wall = WallBarrier3D.Vertical(Vector3.Zero, new Vector3(0f, 0f, 1f), 2f, 3f);
        // Normal projected onto XZ stays horizontal.
        Assert.Equal(0f, wall.Normal.Y, 5);
        // Height axis (bitangent) runs vertically.
        Assert.Equal(1f, MathF.Abs(wall.Bitangent.Y), 5);
        Assert.Equal(3f, wall.HalfExtents.Y);
        AssertOrthonormal(wall);
    }

    [Fact]
    public void HitShape_IsWallPosedAtCenter()
    {
        var wall = WallBarrier3D.Floor(new Vector3(0f, -1f, 0f), new Vector2(4f, 4f));
        var posed = wall.HitShape;
        Assert.Equal(new Vector3(0f, -1f, 0f), posed.Pose.Position);
    }
}
