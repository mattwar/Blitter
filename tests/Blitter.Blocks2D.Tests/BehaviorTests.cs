namespace Blitter.Blocks2D.Tests;

using System.Numerics;

public class BehaviorTests
{
    private static UpdateContext Context(double seconds = 0) =>
        new() { ElapsedSinceLastUpdate = TimeSpan.FromSeconds(seconds) };

    private static UpdateContext Context2D(double seconds = 0) =>
        new() { ElapsedSinceLastUpdate = TimeSpan.FromSeconds(seconds) };

    // ---- WrapInBounds2D ----

    [Fact]
    public void WrapInBounds_WrapsLeftEdgeToRight()
    {
        var sprite = new Sprite2D { Center = new Vector2(-10, 50) };
        sprite.AddTrait(new Bounds2D { Rect = new Rect(0, 0, 100, 100) });
        sprite.AddBehavior(new WrapInBounds2D());
        sprite.Update(Context(0));
        Assert.Equal(new Vector2(90, 50), sprite.Center);
    }

    [Fact]
    public void WrapInBounds_WrapsBottomToTop()
    {
        var sprite = new Sprite2D { Center = new Vector2(50, 110) };
        sprite.AddTrait(new Bounds2D { Rect = new Rect(0, 0, 100, 100) });
        sprite.AddBehavior(new WrapInBounds2D());
        sprite.Update(Context(0));
        Assert.Equal(new Vector2(50, 10), sprite.Center);
    }

    [Fact]
    public void WrapInBounds_InsideBounds_LeavesCenterAlone()
    {
        var sprite = new Sprite2D { Center = new Vector2(50, 50) };
        sprite.AddTrait(new Bounds2D { Rect = new Rect(0, 0, 100, 100) });
        sprite.AddBehavior(new WrapInBounds2D());
        sprite.Update(Context(0));
        Assert.Equal(new Vector2(50, 50), sprite.Center);
    }

    [Fact]
    public void WrapInBounds_InvokesOnWrap()
    {
        var count = 0;
        var sprite = new Sprite2D { Center = new Vector2(-1, 50) };
        sprite.AddTrait(new Bounds2D { Rect = new Rect(0, 0, 100, 100) });
        sprite.AddBehavior(new WrapInBounds2D { OnWrap = _ => count++ });
        sprite.Update(Context(0));
        Assert.Equal(1, count);
    }

    // ---- SeekTarget2D ----

    [Fact]
    public void SeekTarget_AcceleratesUpToMaxSpeed()
    {
        var sprite = new Sprite2D
        {
            Center = Vector2.Zero,
            Behaviors = [new SeekTarget2D { Target = () => new Vector2(0, -100), Acceleration = 50, MaxSpeed = 80, MaxTurnRate = 360 }],
        };
        sprite.Update(Context(1.0));
        Assert.Equal(50f, sprite.Speed, 3);
        sprite.Update(Context(1.0));
        Assert.Equal(80f, sprite.Speed, 3);
    }

    [Fact]
    public void SeekTarget_TurnsTowardTargetCappedByMaxTurnRate()
    {
        var sprite = new Sprite2D
        {
            Center = Vector2.Zero,
            Heading = 0f, // facing up
            Behaviors = [new SeekTarget2D { Target = () => new Vector2(100, 0), MaxTurnRate = 30 }],
        };
        // Desired heading is 90 (right). With MaxTurnRate=30 and 1s dt, heading = 30.
        sprite.Update(Context(1.0));
        Assert.Equal(30f, sprite.Heading, 3);
    }

    [Fact]
    public void SeekTarget_NullTarget_DoesNothing()
    {
        var sprite = new Sprite2D
        {
            Speed = 10,
            Heading = 45,
            Behaviors = [new SeekTarget2D { Target = () => null, Acceleration = 100 }],
        };
        sprite.Update(Context(1.0));
        Assert.Equal(10f, sprite.Speed);
        Assert.Equal(45f, sprite.Heading);
    }

    [Fact]
    public void SeekTarget_InsideArriveRadius_SkipsAccel()
    {
        var sprite = new Sprite2D
        {
            Center = Vector2.Zero,
            Behaviors = [new SeekTarget2D { Target = () => new Vector2(5, 0), Acceleration = 100, ArriveRadius = 10 }],
        };
        sprite.Update(Context(1.0));
        Assert.Equal(0f, sprite.Speed);
    }

    // ---- Timer2D ----

    [Fact]
    public void Timer_FiresAfterDuration()
    {
        var fires = 0;
        var scene = new Scene2D
        {
            Behaviors = [new Timer2D { Duration = TimeSpan.FromSeconds(1), OnExpired = _ => fires++ }],
        };
        scene.Update(Context2D(0.5));
        Assert.Equal(0, fires);
        scene.Update(Context2D(0.6));
        Assert.Equal(1, fires);
    }

    [Fact]
    public void Timer_AutoRestart_FiresRepeatedly()
    {
        var fires = 0;
        var scene = new Scene2D
        {
            Behaviors = [new Timer2D { Duration = TimeSpan.FromSeconds(1), AutoRestart = true, OnExpired = _ => fires++ }],
        };
        scene.Update(Context2D(1.0));
        scene.Update(Context2D(1.0));
        scene.Update(Context2D(1.0));
        Assert.Equal(3, fires);
    }

    [Fact]
    public void Timer_Paused_DoesNotFire()
    {
        var fires = 0;
        var scene = new Scene2D
        {
            Behaviors = [new Timer2D { Duration = TimeSpan.FromSeconds(1), Paused = true, OnExpired = _ => fires++ }],
        };
        scene.Update(Context2D(2.0));
        Assert.Equal(0, fires);
    }

    // ---- TriggerOnPredicate2D ----

    [Fact]
    public void Trigger_FiresOnRisingEdge()
    {
        var fires = 0;
        var flag = false;
        var scene = new Scene2D
        {
            Behaviors = [new TriggerOnPredicate2D { Predicate = _ => flag, Action = _ => fires++ }],
        };
        scene.Update(Context2D());                // false → no fire
        Assert.Equal(0, fires);
        flag = true;
        scene.Update(Context2D());                // false→true → fire
        Assert.Equal(1, fires);
        scene.Update(Context2D());                // true→true → no fire
        Assert.Equal(1, fires);
        flag = false;
        scene.Update(Context2D());
        flag = true;
        scene.Update(Context2D());                // re-arms and fires again
        Assert.Equal(2, fires);
    }

    [Fact]
    public void Trigger_NonRepeating_FiresOnce()
    {
        var fires = 0;
        var flag = false;
        var trig = new TriggerOnPredicate2D { Predicate = _ => flag, Action = _ => fires++, Repeating = false };
        var scene = new Scene2D { Behaviors = [trig] };
        flag = true; scene.Update(Context2D());
        flag = false; scene.Update(Context2D());
        flag = true; scene.Update(Context2D());
        Assert.Equal(1, fires);
    }

    // ---- Shake2D ----

    [Fact]
    public void Shake_NoTrauma_LeavesCenterAlone()
    {
        var sprite = new Sprite2D { Center = new Vector2(10, 20), Behaviors = [new Shake2D()] };
        sprite.Update(Context(0.016));
        Assert.Equal(new Vector2(10, 20), sprite.Center);
    }

    [Fact]
    public void Shake_TraumaDecaysToZero()
    {
        var shake = new Shake2D { Decay = 1f };
        shake.AddTrauma(1f);
        var sprite = new Sprite2D { Behaviors = [shake] };
        // 2 seconds at decay 1/s should fully drain trauma.
        sprite.Update(Context(2.0));
        Assert.Equal(0f, shake.Trauma, 3);
    }

    [Fact]
    public void Shake_OffsetReturnsToZeroAfterDecay()
    {
        var shake = new Shake2D { Decay = 1f, MaxOffset = 100f };
        shake.AddTrauma(1f);
        var sprite = new Sprite2D { Center = new Vector2(50, 50), Behaviors = [shake] };
        // Drain completely.
        sprite.Update(Context(10.0));
        Assert.Equal(new Vector2(50, 50), sprite.Center);
    }

    // ---- CameraShake2D ----

    [Fact]
    public void CameraShake_NoTrauma_LeavesCameraAlone()
    {
        var cam = new Camera2D { Position = new Vector2(10, 20) };
        var sprite = new Sprite2D { Behaviors = [new CameraShake2D { Camera = cam }] };
        sprite.Update(Context(0.016));
        Assert.Equal(new Vector2(10, 20), cam.Position);
    }

    [Fact]
    public void CameraShake_DetectsExternalBaselineChange()
    {
        var cam = new Camera2D { Position = new Vector2(0, 0) };
        var shake = new CameraShake2D { Camera = cam, Decay = 0f, MaxOffset = 0f };
        shake.AddTrauma(1f);
        var sprite = new Sprite2D { Behaviors = [shake] };

        sprite.Update(Context(0.016));
        // External writer moves camera between ticks:
        cam.Position = new Vector2(100, 100);
        sprite.Update(Context(0.016));
        // With MaxOffset=0 the shake adds nothing, so camera should be at baseline.
        Assert.Equal(new Vector2(100, 100), cam.Position);
    }
}
