using System.Numerics;

namespace Blitter.Tests;

public class Gravity3DTests
{
    private static UpdateContext3D Ctx(double dt) => new()
    {
        ElapsedSinceStart = TimeSpan.FromSeconds(dt),
        ElapsedSinceLastUpdate = TimeSpan.FromSeconds(dt),
    };

    [Fact]
    public void AppliesAccelerationToVelocity()
    {
        var sprite = new Sprite3D();
        var gravity = new Gravity3D { Acceleration = new Vector3(0f, -10f, 0f) };

        gravity.Apply(sprite, Ctx(0.5));

        Assert.Equal(new Vector3(0f, -5f, 0f), sprite.Velocity);
    }

    [Fact]
    public void Accumulates_OverMultipleFrames()
    {
        var sprite = new Sprite3D();
        var gravity = new Gravity3D { Acceleration = new Vector3(0f, -10f, 0f) };

        gravity.Apply(sprite, Ctx(0.1));
        gravity.Apply(sprite, Ctx(0.1));

        Assert.Equal(-2f, sprite.Velocity.Y, 5);
    }

    [Fact]
    public void ZeroElapsed_DoesNothing()
    {
        var sprite = new Sprite3D { Velocity = new Vector3(1f, 2f, 3f) };
        var gravity = new Gravity3D();

        gravity.Apply(sprite, Ctx(0));

        Assert.Equal(new Vector3(1f, 2f, 3f), sprite.Velocity);
    }

    [Fact]
    public void MaxFallSpeed_CapsAlongGravityAxis()
    {
        var sprite = new Sprite3D { Velocity = new Vector3(0f, -8f, 0f) };
        var gravity = new Gravity3D
        {
            Acceleration = new Vector3(0f, -10f, 0f),
            MaxFallSpeed = 9f,
        };

        // -8 + (-10 * 0.5) = -13, capped to -9.
        gravity.Apply(sprite, Ctx(0.5));

        Assert.Equal(-9f, sprite.Velocity.Y, 5);
    }

    [Fact]
    public void MaxFallSpeed_DoesNotAffectPerpendicularMotion()
    {
        var sprite = new Sprite3D { Velocity = new Vector3(5f, -20f, 0f) };
        var gravity = new Gravity3D
        {
            Acceleration = new Vector3(0f, -10f, 0f),
            MaxFallSpeed = 9f,
        };

        gravity.Apply(sprite, Ctx(0.1));

        // Horizontal component is untouched; vertical is capped.
        Assert.Equal(5f, sprite.Velocity.X, 5);
        Assert.Equal(-9f, sprite.Velocity.Y, 5);
    }

    [Fact]
    public void BelowMaxFallSpeed_IsNotClamped()
    {
        var sprite = new Sprite3D();
        var gravity = new Gravity3D
        {
            Acceleration = new Vector3(0f, -10f, 0f),
            MaxFallSpeed = 100f,
        };

        gravity.Apply(sprite, Ctx(0.1));

        Assert.Equal(-1f, sprite.Velocity.Y, 5);
    }

    [Fact]
    public void ZeroAcceleration_DoesNothing()
    {
        var sprite = new Sprite3D { Velocity = new Vector3(1f, 1f, 1f) };
        var gravity = new Gravity3D { Acceleration = Vector3.Zero };

        gravity.Apply(sprite, Ctx(0.1));

        Assert.Equal(new Vector3(1f, 1f, 1f), sprite.Velocity);
    }
}
