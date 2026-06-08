using System.Numerics;

namespace Blitter.Tests;

public class Spin3DTests
{
    private static UpdateContext3D Ctx(double dt) => new()
    {
        ElapsedSinceStart = TimeSpan.FromSeconds(dt),
        ElapsedSinceLastUpdate = TimeSpan.FromSeconds(dt),
    };

    [Fact]
    public void RotatesAroundYAxis()
    {
        var sprite = new Sprite3D();
        var spin = new Spin3D { RotationSpeed = MathF.PI }; // half turn per second

        spin.Apply(sprite, Ctx(1.0));

        // Forward -Z rotated half a turn about Y lands on +Z.
        var forward = Vector3.Transform(-Vector3.UnitZ, sprite.Orientation);
        Assert.Equal(0f, forward.X, 4);
        Assert.Equal(1f, forward.Z, 4);
    }

    [Fact]
    public void KeepsYAxisFixed()
    {
        var sprite = new Sprite3D();
        var spin = new Spin3D { RotationSpeed = 1.23f };

        spin.Apply(sprite, Ctx(0.7));

        // Spinning about Y leaves the up axis unmoved.
        var up = Vector3.Transform(Vector3.UnitY, sprite.Orientation);
        Assert.Equal(0f, up.X, 5);
        Assert.Equal(1f, up.Y, 5);
        Assert.Equal(0f, up.Z, 5);
    }

    [Fact]
    public void ZeroSpeed_DoesNothing()
    {
        var sprite = new Sprite3D();
        var spin = new Spin3D { RotationSpeed = 0f };

        spin.Apply(sprite, Ctx(1.0));

        Assert.Equal(Quaternion.Identity, sprite.Orientation);
    }

    [Fact]
    public void ZeroElapsed_DoesNothing()
    {
        var sprite = new Sprite3D();
        var spin = new Spin3D { RotationSpeed = 5f };

        spin.Apply(sprite, Ctx(0));

        Assert.Equal(Quaternion.Identity, sprite.Orientation);
    }

    [Fact]
    public void Accumulates_OverFrames()
    {
        var oneStep = new Sprite3D();
        var twoSteps = new Sprite3D();
        new Spin3D { RotationSpeed = 1f }.Apply(oneStep, Ctx(0.5));

        var spin = new Spin3D { RotationSpeed = 1f };
        spin.Apply(twoSteps, Ctx(0.25));
        spin.Apply(twoSteps, Ctx(0.25));

        // Two quarter-second steps equal one half-second step.
        var a = Vector3.Transform(-Vector3.UnitZ, oneStep.Orientation);
        var b = Vector3.Transform(-Vector3.UnitZ, twoSteps.Orientation);
        Assert.Equal(a.X, b.X, 4);
        Assert.Equal(a.Z, b.Z, 4);
    }
}
