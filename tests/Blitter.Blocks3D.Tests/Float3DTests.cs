using System.Numerics;

namespace Blitter.Tests;

public class Float3DTests
{
    private static UpdateContext3D Ctx(double dt) => new()
    {
        ElapsedSinceStart = TimeSpan.FromSeconds(dt),
        ElapsedSinceLastUpdate = TimeSpan.FromSeconds(dt),
    };

    [Fact]
    public void OffsetsYBySineOfElapsedTimes()
    {
        var sprite = new Sprite3D { Position = new Vector3(0f, 10f, 0f) };
        var bob = new Float3D { Amplitude = 2f, Frequency = 1f };

        bob.Apply(sprite, Ctx(0.5));

        var expected = 10f + MathF.Sin(0.5f) * 2f;
        Assert.Equal(expected, sprite.Position.Y, 5);
    }

    [Fact]
    public void LeavesXAndZUnchanged()
    {
        var sprite = new Sprite3D { Position = new Vector3(3f, 0f, 7f) };
        var bob = new Float3D();

        bob.Apply(sprite, Ctx(0.25));

        Assert.Equal(3f, sprite.Position.X);
        Assert.Equal(7f, sprite.Position.Z);
    }

    [Fact]
    public void ZeroElapsed_LeavesYUnchanged()
    {
        var sprite = new Sprite3D { Position = new Vector3(0f, 5f, 0f) };
        var bob = new Float3D { Amplitude = 2f, Frequency = 3f };

        bob.Apply(sprite, Ctx(0));

        // sin(0) == 0, so no offset.
        Assert.Equal(5f, sprite.Position.Y, 5);
    }

    [Fact]
    public void Amplitude_ScalesOffset()
    {
        var spriteSmall = new Sprite3D { Position = Vector3.Zero };
        var spriteLarge = new Sprite3D { Position = Vector3.Zero };
        new Float3D { Amplitude = 1f, Frequency = 1f }.Apply(spriteSmall, Ctx(0.5));
        new Float3D { Amplitude = 3f, Frequency = 1f }.Apply(spriteLarge, Ctx(0.5));

        Assert.Equal(spriteSmall.Position.Y * 3f, spriteLarge.Position.Y, 5);
    }
}
