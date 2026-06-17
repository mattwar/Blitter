using System.Numerics;

namespace Blitter.Tests;

public class Motion3DTests
{
    private static UpdateContext Ctx(double dt) => new()
    {
        ElapsedSinceStart = TimeSpan.FromSeconds(dt),
        ElapsedSinceLastUpdate = TimeSpan.FromSeconds(dt),
    };

    [Fact]
    public void IntegratesVelocityIntoPosition()
    {
        var motion = new Motion3D();
        var sprite = new Sprite3D { Velocity = new Vector3(2f, 0f, 0f), Behaviors = [ motion ] };

        motion.Apply(Ctx(0.5));

        Assert.Equal(new Vector3(1f, 0f, 0f), sprite.Position);
    }

    [Fact]
    public void ZeroElapsed_DoesNothing()
    {
        var motion = new Motion3D();
        var sprite = new Sprite3D
        {
            Position = new Vector3(5f, 5f, 5f),
            Velocity = new Vector3(2f, 0f, 0f),
            Behaviors = [ motion ],
        };

        motion.Apply(Ctx(0));

        Assert.Equal(new Vector3(5f, 5f, 5f), sprite.Position);
    }

    [Fact]
    public void SmallDeltasBelowInterval_AreBufferedThenApplied()
    {
        var motion = new Motion3D { MinUpdateInterval = TimeSpan.FromMilliseconds(10) };
        var sprite = new Sprite3D { Velocity = new Vector3(10f, 0f, 0f), Behaviors = [ motion ] };

        // Each step is under the 10ms interval, so nothing moves yet.
        motion.Apply(Ctx(0.004));
        Assert.Equal(Vector3.Zero, sprite.Position);
        motion.Apply(Ctx(0.004));
        Assert.Equal(Vector3.Zero, sprite.Position);

        // Third step crosses the threshold; the full 12ms is integrated.
        motion.Apply(Ctx(0.004));
        Assert.Equal(10f * 0.012f, sprite.Position.X, 5);
    }

    [Fact]
    public void IntegratesAngularVelocityIntoOrientation()
    {
        var motion = new Motion3D();
        var sprite = new Sprite3D
        {
            AngularVelocity = new Vector3(0f, MathF.PI, 0f), // pi rad/s about Y
            Behaviors = [ motion ],
        };

        motion.Apply(Ctx(1.0)); // one second => half turn

        // -Z forward rotated half a turn about Y lands on +Z.
        var forward = Vector3.Transform(-Vector3.UnitZ, sprite.Orientation);
        Assert.Equal(0f, forward.X, 4);
        Assert.Equal(0f, forward.Y, 4);
        Assert.Equal(1f, forward.Z, 4);
    }

    [Fact]
    public void ZeroVelocityAndAngular_LeavesTransformUnchanged()
    {
        var motion = new Motion3D();
        var sprite = new Sprite3D { Position = new Vector3(1f, 2f, 3f), Behaviors = [ motion ] };

        motion.Apply(Ctx(0.1));

        Assert.Equal(new Vector3(1f, 2f, 3f), sprite.Position);
        Assert.Equal(Quaternion.Identity, sprite.Orientation);
    }
}
