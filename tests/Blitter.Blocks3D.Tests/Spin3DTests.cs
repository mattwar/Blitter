using System.Numerics;

namespace Blitter.Tests;

public class Spin3DTests
{
    private static UpdateContext Ctx(double dt) => new()
    {
        ElapsedSinceStart = TimeSpan.FromSeconds(dt),
        ElapsedSinceLastUpdate = TimeSpan.FromSeconds(dt),
    };

    [Fact]
    public void RotatesAroundYAxis()
    {
        var spin = new Spin3D { RotationSpeed = MathF.PI }; // half turn per second
        var sprite = new Sprite3D { Behaviors = [ spin ] };

        spin.Update(Ctx(1.0));

        // Forward -Z rotated half a turn about Y lands on +Z.
        var forward = Vector3.Transform(-Vector3.UnitZ, sprite.Orientation);
        Assert.Equal(0f, forward.X, 4);
        Assert.Equal(1f, forward.Z, 4);
    }

    [Fact]
    public void KeepsYAxisFixed()
    {
        var spin = new Spin3D { RotationSpeed = 1.23f };
        var sprite = new Sprite3D { Behaviors = [ spin ] };

        spin.Update(Ctx(0.7));

        // Spinning about Y leaves the up axis unmoved.
        var up = Vector3.Transform(Vector3.UnitY, sprite.Orientation);
        Assert.Equal(0f, up.X, 5);
        Assert.Equal(1f, up.Y, 5);
        Assert.Equal(0f, up.Z, 5);
    }

    [Fact]
    public void ZeroSpeed_DoesNothing()
    {
        var spin = new Spin3D { RotationSpeed = 0f };
        var sprite = new Sprite3D { Behaviors = [ spin ] };

        spin.Update(Ctx(1.0));

        Assert.Equal(Quaternion.Identity, sprite.Orientation);
    }

    [Fact]
    public void ZeroElapsed_DoesNothing()
    {
        var spin = new Spin3D { RotationSpeed = 5f };
        var sprite = new Sprite3D { Behaviors = [ spin ] };

        spin.Update(Ctx(0));

        Assert.Equal(Quaternion.Identity, sprite.Orientation);
    }

    [Fact]
    public void Accumulates_OverFrames()
    {
        var oneSpin = new Spin3D { RotationSpeed = 1f };
        var oneStep = new Sprite3D { Behaviors = [ oneSpin ] };
        oneSpin.Update(Ctx(0.5));

        var spin = new Spin3D { RotationSpeed = 1f };
        var twoSteps = new Sprite3D { Behaviors = [ spin ] };
        spin.Update(Ctx(0.25));
        spin.Update(Ctx(0.25));

        // Two quarter-second steps equal one half-second step.
        var a = Vector3.Transform(-Vector3.UnitZ, oneStep.Orientation);
        var b = Vector3.Transform(-Vector3.UnitZ, twoSteps.Orientation);
        Assert.Equal(a.X, b.X, 4);
        Assert.Equal(a.Z, b.Z, 4);
    }
}
