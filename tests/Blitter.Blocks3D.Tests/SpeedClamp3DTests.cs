using System.Numerics;

namespace Blitter.Tests;

public class SpeedClamp3DTests
{
    private static readonly UpdateContext Ctx = new()
    {
        ElapsedSinceStart = TimeSpan.FromSeconds(0.1),
        ElapsedSinceLastUpdate = TimeSpan.FromSeconds(0.1),
    };

    [Fact]
    public void FasterThanMax_IsPulledDown()
    {
        var clamp = new SpeedClamp3D { Max = 4f };
        var sprite = new Sprite3D { Velocity = new Vector3(10f, 0f, 0f), Behaviors = [ clamp ] };

        clamp.Update(Ctx);

        Assert.Equal(4f, sprite.Velocity.Length(), 5);
        Assert.Equal(new Vector3(4f, 0f, 0f), sprite.Velocity);
    }

    [Fact]
    public void SlowerThanMin_IsPushedUp()
    {
        var clamp = new SpeedClamp3D { Min = 5f };
        var sprite = new Sprite3D { Velocity = new Vector3(0f, 0f, 1f), Behaviors = [ clamp ] };

        clamp.Update(Ctx);

        Assert.Equal(5f, sprite.Velocity.Length(), 5);
        Assert.Equal(new Vector3(0f, 0f, 5f), sprite.Velocity);
    }

    [Fact]
    public void WithinRange_IsUnchanged()
    {
        var clamp = new SpeedClamp3D { Min = 1f, Max = 5f };
        var sprite = new Sprite3D { Velocity = new Vector3(3f, 0f, 0f), Behaviors = [ clamp ] };

        clamp.Update(Ctx);

        Assert.Equal(new Vector3(3f, 0f, 0f), sprite.Velocity);
    }

    [Fact]
    public void PreservesDirection_WhenClampingMax()
    {
        var v = new Vector3(3f, 4f, 0f); // length 5
        var clamp = new SpeedClamp3D { Max = 1f };
        var sprite = new Sprite3D { Velocity = v, Behaviors = [ clamp ] };

        clamp.Update(Ctx);

        var expected = Vector3.Normalize(v);
        Assert.Equal(expected.X, Vector3.Normalize(sprite.Velocity).X, 5);
        Assert.Equal(expected.Y, Vector3.Normalize(sprite.Velocity).Y, 5);
        Assert.Equal(1f, sprite.Velocity.Length(), 5);
    }

    [Fact]
    public void ZeroVelocity_IsLeftAlone()
    {
        var clamp = new SpeedClamp3D { Min = 5f, Max = 10f };
        var sprite = new Sprite3D { Velocity = Vector3.Zero, Behaviors = [ clamp ] };

        clamp.Update(Ctx);

        Assert.Equal(Vector3.Zero, sprite.Velocity);
    }

    [Fact]
    public void ZeroMin_DoesNotPushUpSlowSprite()
    {
        var clamp = new SpeedClamp3D { Min = 0f, Max = 10f };
        var sprite = new Sprite3D { Velocity = new Vector3(0.1f, 0f, 0f), Behaviors = [ clamp ] };

        clamp.Update(Ctx);

        Assert.Equal(new Vector3(0.1f, 0f, 0f), sprite.Velocity);
    }
}
