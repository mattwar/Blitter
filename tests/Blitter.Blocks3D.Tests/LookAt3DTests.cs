using System.Numerics;

namespace Blitter.Tests;

public class LookAt3DTests
{
    private static UpdateContext Ctx(double dt) => new()
    {
        ElapsedSinceStart = TimeSpan.FromSeconds(dt),
        ElapsedSinceLastUpdate = TimeSpan.FromSeconds(dt),
    };

    private static Vector3 ForwardOf(Sprite3D s) =>
        Vector3.Transform(-Vector3.UnitZ, s.Orientation);

    [Fact]
    public void TargetPoint_AimsForwardAtTarget()
    {
        var look = new TestLookAt3D { TargetPoint = new Vector3(1f, 0f, 0f) };
        var sprite = new Sprite3D { Position = Vector3.Zero, Behaviors = [ look ] };

        look.Update(Ctx(0.016));

        var fwd = ForwardOf(sprite);
        Assert.Equal(1f, fwd.X, 4);
        Assert.Equal(0f, fwd.Y, 4);
        Assert.Equal(0f, fwd.Z, 4);
    }

    [Fact]
    public void TargetSprite_TakesPriorityOverPoint()
    {
        var targetSprite = new Sprite3D { Position = new Vector3(0f, 0f, -1f) };
        var look = new TestLookAt3D
        {
            Target = targetSprite,
            TargetPoint = new Vector3(1f, 0f, 0f),
        };
        var sprite = new Sprite3D { Position = Vector3.Zero, Behaviors = [ look ] };

        look.Update(Ctx(0.016));

        // Faces the sprite at -Z, not the point at +X.
        var fwd = ForwardOf(sprite);
        Assert.Equal(0f, fwd.X, 4);
        Assert.Equal(-1f, fwd.Z, 4);
    }

    [Fact]
    public void TargetSelector_UsedWhenNoSpriteOrPoint()
    {
        var look = new TestLookAt3D
        {
            SelectedTarget = new Vector3(0f, 0f, -1f),
        };
        var sprite = new Sprite3D { Position = Vector3.Zero, Behaviors = [ look ] };

        look.Update(Ctx(0.016));

        Assert.Equal(-1f, ForwardOf(sprite).Z, 4);
    }

    [Fact]
    public void NoTarget_LeavesOrientationUnchanged()
    {
        var look = new TestLookAt3D();
        var sprite = new Sprite3D { Position = Vector3.Zero, Behaviors = [ look ] };

        look.Update(Ctx(0.016));

        Assert.Equal(Quaternion.Identity, sprite.Orientation);
    }

    [Fact]
    public void SelectorReturningNull_SkipsTurn()
    {
        var look = new TestLookAt3D { SelectedTarget = null };
        var sprite = new Sprite3D { Position = Vector3.Zero, Behaviors = [ look ] };

        look.Update(Ctx(0.016));

        Assert.Equal(Quaternion.Identity, sprite.Orientation);
    }

    [Fact]
    public void KeepUpright_IgnoresHeightDifference()
    {
        var look = new TestLookAt3D
        {
            TargetPoint = new Vector3(1f, 5f, 0f),
            KeepUpright = true,
        };
        var sprite = new Sprite3D { Position = Vector3.Zero, Behaviors = [ look ] };

        look.Update(Ctx(0.016));

        // Forward stays level despite the target being above.
        Assert.Equal(0f, ForwardOf(sprite).Y, 4);
    }

    [Fact]
    public void TargetOnTopOfSprite_SkipsTurn()
    {
        var look = new TestLookAt3D { TargetPoint = new Vector3(2f, 2f, 2f) };
        var sprite = new Sprite3D { Position = new Vector3(2f, 2f, 2f), Behaviors = [ look ] };

        look.Update(Ctx(0.016));

        Assert.Equal(Quaternion.Identity, sprite.Orientation);
    }

    [Fact]
    public void TurnSpeed_EasesTowardTargetWithoutSnapping()
    {
        // Start facing -Z (identity); target is behind at +Z (180 deg away).
        var look = new TestLookAt3D
        {
            TargetPoint = new Vector3(0f, 0f, 1f),
            TurnSpeed = 0.1f, // very slow
        };
        var sprite = new Sprite3D { Position = Vector3.Zero, Behaviors = [ look ] };

        look.Update(Ctx(0.1)); // maxStep = 0.01 rad, far short of pi

        // Should have rotated only a little — not snapped to face +Z.
        var fwd = ForwardOf(sprite);
        Assert.True(fwd.Z < 0f, "Slow turn should not flip all the way to +Z in one step.");
    }

    [Fact]
    public void TurnSpeed_LargeStep_SnapsToTarget()
    {
        var look = new TestLookAt3D
        {
            TargetPoint = new Vector3(1f, 0f, 0f),
            TurnSpeed = 100f, // huge — step exceeds remaining angle
        };
        var sprite = new Sprite3D { Position = Vector3.Zero, Behaviors = [ look ] };

        look.Update(Ctx(1.0));

        Assert.Equal(1f, ForwardOf(sprite).X, 4);
    }
}

file sealed class TestLookAt3D : LookAt3D
{
    public Vector3? SelectedTarget { get; init; }
    protected override Vector3? SelectTarget() => SelectedTarget;
}
