namespace Blitter.Blocks2D.Tests;

using System.Numerics;
using Timer = Blitter.Blocks.Timer;

public class BehaviorTests
{
    private static EntityUpdateContext Context(double seconds = 0) =>
        new() { ElapsedSinceLastUpdate = TimeSpan.FromSeconds(seconds) };

    private static EntityUpdateContext Context2D(double seconds = 0) =>
        new() { ElapsedSinceLastUpdate = TimeSpan.FromSeconds(seconds) };

    // ---- WrapInBounds2D ----

    [Fact]
    public void WrapInBounds_WrapsLeftEdgeToRight()
    {
        var sprite = new Sprite2D { Center = new Vector2(-10, 50) };
        sprite.GetOrAddTrait<Bounds2D>().Rect = new Rect(0, 0, 100, 100);
        sprite.GetOrAddBehavior<WrapInBounds2D>();
        Updater.Default.UpdateEntity(sprite, Context(0));
        Assert.Equal(new Vector2(90, 50), sprite.Center);
    }

    [Fact]
    public void WrapInBounds_WrapsBottomToTop()
    {
        var sprite = new Sprite2D { Center = new Vector2(50, 110) };
        sprite.GetOrAddTrait<Bounds2D>().Rect = new Rect(0, 0, 100, 100);
        sprite.GetOrAddBehavior<WrapInBounds2D>();
        Updater.Default.UpdateEntity(sprite, Context(0));
        Assert.Equal(new Vector2(50, 10), sprite.Center);
    }

    [Fact]
    public void WrapInBounds_InsideBounds_LeavesCenterAlone()
    {
        var sprite = new Sprite2D { Center = new Vector2(50, 50) };
        sprite.GetOrAddTrait<Bounds2D>().Rect = new Rect(0, 0, 100, 100);
        sprite.GetOrAddBehavior<WrapInBounds2D>();
        Updater.Default.UpdateEntity(sprite, Context(0));
        Assert.Equal(new Vector2(50, 50), sprite.Center);
    }

    [Fact]
    public void WrapInBounds_InvokesOnWrap()
    {
        var recorder = new EventRecorder<Wrapped2DEventArgs>();
        var sprite = new Sprite2D { Center = new Vector2(-1, 50) };
        sprite.GetOrAddTrait<Bounds2D>().Rect = new Rect(0, 0, 100, 100);
        sprite.GetOrAddBehavior<WrapInBounds2D>().Wrapped = recorder;
        Updater.Default.UpdateEntity(sprite, Context(0));
        Assert.Equal(1, recorder.Count);
    }

    // ---- SeekTarget2D ----

    [Fact]
    public void SeekTarget_AcceleratesUpToMaxSpeed()
    {
        var sprite = new Sprite2D
        {
            Center = Vector2.Zero,
            Behaviors = [new TestSeekTarget2D { Target = new Vector2(0, -100), Acceleration = 50, MaxSpeed = 80, MaxTurnRate = 360 }],
        };
        Updater.Default.UpdateEntity(sprite, Context(1.0));
        Assert.Equal(50f, sprite.Speed, 3);
        Updater.Default.UpdateEntity(sprite, Context(1.0));
        Assert.Equal(80f, sprite.Speed, 3);
    }

    [Fact]
    public void SeekTarget_TurnsTowardTargetCappedByMaxTurnRate()
    {
        var sprite = new Sprite2D
        {
            Center = Vector2.Zero,
            Heading = 0f, // facing up
            Behaviors = [new TestSeekTarget2D { Target = new Vector2(100, 0), MaxTurnRate = 30 }],
        };
        // Desired heading is 90 (right). With MaxTurnRate=30 and 1s dt, heading = 30.
        Updater.Default.UpdateEntity(sprite, Context(1.0));
        Assert.Equal(30f, sprite.Heading, 3);
    }

    [Fact]
    public void SeekTarget_NullTarget_DoesNothing()
    {
        var sprite = new Sprite2D
        {
            Speed = 10,
            Heading = 45,
            Behaviors = [new TestSeekTarget2D { Target = null, Acceleration = 100 }],
        };
        Updater.Default.UpdateEntity(sprite, Context(1.0));
        Assert.Equal(10f, sprite.Speed);
        Assert.Equal(45f, sprite.Heading);
    }

    [Fact]
    public void SeekTarget_InsideArriveRadius_SkipsAccel()
    {
        var sprite = new Sprite2D
        {
            Center = Vector2.Zero,
            Behaviors = [new TestSeekTarget2D { Target = new Vector2(5, 0), Acceleration = 100, ArriveRadius = 10 }],
        };
        Updater.Default.UpdateEntity(sprite, Context(1.0));
        Assert.Equal(0f, sprite.Speed);
    }

    // ---- Timer ----

    [Fact]
    public void Timer_FiresAfterDuration()
    {
        var recorder = new EventRecorder<TimerExpiredEventArgs>();
        var scene = new Scene2D
        {
            Behaviors = [new Timer { Duration = TimeSpan.FromSeconds(1), Expired = recorder }],
        };
        Updater.Default.Update(scene, Context2D(0.5));
        Assert.Equal(0, recorder.Count);
        Updater.Default.Update(scene, Context2D(0.6));
        Assert.Equal(1, recorder.Count);
    }

    [Fact]
    public void Timer_AutoRestart_FiresRepeatedly()
    {
        var recorder = new EventRecorder<TimerExpiredEventArgs>();
        var scene = new Scene2D
        {
            Behaviors = [new Timer { Duration = TimeSpan.FromSeconds(1), AutoRestart = true, Expired = recorder }],
        };
        Updater.Default.Update(scene, Context2D(1.0));
        Updater.Default.Update(scene, Context2D(1.0));
        Updater.Default.Update(scene, Context2D(1.0));
        Assert.Equal(3, recorder.Count);
    }

    [Fact]
    public void Timer_Paused_DoesNotFire()
    {
        var recorder = new EventRecorder<TimerExpiredEventArgs>();
        var scene = new Scene2D
        {
            Behaviors = [new Timer { Duration = TimeSpan.FromSeconds(1), Paused = true, Expired = recorder }],
        };
        Updater.Default.Update(scene, Context2D(2.0));
        Assert.Equal(0, recorder.Count);
    }

    // ---- TriggerOnPredicate2D ----

    [Fact]
    public void Trigger_FiresOnRisingEdge()
    {
        var trigger = new TestTriggerOnPredicate2D();
        var scene = new Scene2D
        {
            Behaviors = [trigger],
        };
        Updater.Default.Update(scene, Context2D());                // false → no fire
        Assert.Equal(0, trigger.FireCount);
        trigger.Flag = true;
        Updater.Default.Update(scene, Context2D());                // false→true → fire
        Assert.Equal(1, trigger.FireCount);
        Updater.Default.Update(scene, Context2D());                // true→true → no fire
        Assert.Equal(1, trigger.FireCount);
        trigger.Flag = false;
        Updater.Default.Update(scene, Context2D());
        trigger.Flag = true;
        Updater.Default.Update(scene, Context2D());                // re-arms and fires again
        Assert.Equal(2, trigger.FireCount);
    }

    [Fact]
    public void Trigger_NonRepeating_FiresOnce()
    {
        var trig = new TestTriggerOnPredicate2D { Repeating = false };
        var scene = new Scene2D { Behaviors = [trig] };
        trig.Flag = true; Updater.Default.Update(scene, Context2D());
        trig.Flag = false; Updater.Default.Update(scene, Context2D());
        trig.Flag = true; Updater.Default.Update(scene, Context2D());
        Assert.Equal(1, trig.FireCount);
    }

    // ---- Shake2D ----

    [Fact]
    public void Shake_NoTrauma_LeavesCenterAlone()
    {
        var sprite = new Sprite2D { Center = new Vector2(10, 20), Behaviors = [new Shake2D()] };
        Updater.Default.UpdateEntity(sprite, Context(0.016));
        Assert.Equal(new Vector2(10, 20), sprite.Center);
    }

    [Fact]
    public void Shake_TraumaDecaysToZero()
    {
        var shake = new Shake2D { Decay = 1f };
        shake.AddTrauma(1f);
        var sprite = new Sprite2D { Behaviors = [shake] };
        // 2 seconds at decay 1/s should fully drain trauma.
        Updater.Default.UpdateEntity(sprite, Context(2.0));
        Assert.Equal(0f, shake.Trauma, 3);
    }

    [Fact]
    public void Shake_OffsetReturnsToZeroAfterDecay()
    {
        var shake = new Shake2D { Decay = 1f, MaxOffset = 100f };
        shake.AddTrauma(1f);
        var sprite = new Sprite2D { Center = new Vector2(50, 50), Behaviors = [shake] };
        // Drain completely.
        Updater.Default.UpdateEntity(sprite, Context(10.0));
        Assert.Equal(new Vector2(50, 50), sprite.Center);
    }

    // ---- CameraShake2D ----

    [Fact]
    public void CameraShake_NoTrauma_LeavesCameraAlone()
    {
        var cam = new Camera2D { Position = new Vector2(10, 20) };
        var sprite = new Sprite2D { Behaviors = [new CameraShake2D { Camera = cam }] };
        Updater.Default.UpdateEntity(sprite, Context(0.016));
        Assert.Equal(new Vector2(10, 20), cam.Position);
    }

    [Fact]
    public void CameraShake_DetectsExternalBaselineChange()
    {
        var cam = new Camera2D { Position = new Vector2(0, 0) };
        var shake = new CameraShake2D { Camera = cam, Decay = 0f, MaxOffset = 0f };
        shake.AddTrauma(1f);
        var sprite = new Sprite2D { Behaviors = [shake] };

        Updater.Default.UpdateEntity(sprite, Context(0.016));
        // External writer moves camera between ticks:
        cam.Position = new Vector2(100, 100);
        Updater.Default.UpdateEntity(sprite, Context(0.016));
        // With MaxOffset=0 the shake adds nothing, so camera should be at baseline.
        Assert.Equal(new Vector2(100, 100), cam.Position);
    }
}

file sealed class EventRecorder<T> : IEventHandler<T>
{
    public int Count { get; private set; }
    public T Last { get; private set; } = default!;
    public void OnEvent(in T args)
    {
        Count++;
        Last = args;
    }
}

file sealed class TestSeekTarget2D : SeekTarget2D
{
    public Vector2? Target { get; init; }
    protected override Vector2? ResolveTarget() => Target;
}

file sealed class TestTriggerOnPredicate2D : TriggerOnPredicate2D
{
    public bool Flag { get; set; }
    public int FireCount { get; private set; }

    protected override bool IsTriggered(IEntity entity) => Flag;
    protected override void OnTriggered(IEntity entity) => FireCount++;
}
