using System.Numerics;

namespace Blitter.Tests;

public class BarrierBounce3DTests
{
    private static readonly UpdateContext Ctx = new()
    {
        ElapsedSinceStart = TimeSpan.FromSeconds(0.1),
        ElapsedSinceLastUpdate = TimeSpan.FromSeconds(0.1),
    };

    // Ball of radius 0.5 just overlapping a sphere barrier of radius 1 at
    // the origin: centers 1.4 apart, radii sum 1.5 => penetration 0.1,
    // contact normal +X (from barrier toward ball).
    private static (BallSprite3D ball, SphereBarrier3D barrier) OverlappingPair(Vector3 velocity)
    {
        var ball = new BallSprite3D
        {
            Radius = 0.5f,
            Position = new Vector3(1.4f, 0f, 0f),
            Velocity = velocity,
        };
        var barrier = new SphereBarrier3D(Vector3.Zero, 1f);
        return (ball, barrier);
    }

    [Fact]
    public void ReflectsVelocityOnHeadOnHit()
    {
        var (ball, barrier) = OverlappingPair(new Vector3(-5f, 0f, 0f));
        var bounce = new BarrierBounce3D();

        bounce.OnHitBarrier(ball, barrier, Ctx);

        // Perfectly elastic reflection off the +X normal.
        Assert.Equal(5f, ball.Velocity.X, 4);
        Assert.Equal(0f, ball.Velocity.Y, 4);
        Assert.Equal(0f, ball.Velocity.Z, 4);
    }

    [Fact]
    public void PushesSpriteOutOfPenetration()
    {
        var (ball, barrier) = OverlappingPair(new Vector3(-5f, 0f, 0f));
        var bounce = new BarrierBounce3D();

        bounce.OnHitBarrier(ball, barrier, Ctx);

        // Pushed out along +X by the 0.1 penetration depth.
        Assert.Equal(1.5f, ball.Position.X, 4);
    }

    [Fact]
    public void Restitution_ScalesNormalRebound()
    {
        var (ball, barrier) = OverlappingPair(new Vector3(-10f, 0f, 0f));
        var bounce = new BarrierBounce3D { Restitution = 0.5f };

        bounce.OnHitBarrier(ball, barrier, Ctx);

        // Half-elastic: rebound speed halved.
        Assert.Equal(5f, ball.Velocity.X, 4);
    }

    [Fact]
    public void SeparatingSprite_IsNotReflected()
    {
        // Velocity points away from the barrier (+X); along >= 0, no bounce.
        var (ball, barrier) = OverlappingPair(new Vector3(5f, 0f, 0f));
        var bounce = new BarrierBounce3D();

        bounce.OnHitBarrier(ball, barrier, Ctx);

        // Velocity unchanged (still moving out), but still depenetrated.
        Assert.Equal(5f, ball.Velocity.X, 4);
    }

    [Fact]
    public void TangentialVelocity_IsPreservedWhenFrictionless()
    {
        var (ball, barrier) = OverlappingPair(new Vector3(-5f, 3f, 0f));
        var bounce = new BarrierBounce3D();

        bounce.OnHitBarrier(ball, barrier, Ctx);

        // Normal (X) reflects; tangential (Y) is retained.
        Assert.Equal(5f, ball.Velocity.X, 4);
        Assert.Equal(3f, ball.Velocity.Y, 4);
    }

    [Fact]
    public void OnBounceCallback_FiresWithContactNormal()
    {
        var (ball, barrier) = OverlappingPair(new Vector3(-5f, 0f, 0f));
        var recorder = new EventRecorder<BarrierBounced3DEventArgs>();
        var bounce = new BarrierBounce3D { Bounced = recorder };

        bounce.OnHitBarrier(ball, barrier, Ctx);

        Assert.Equal(1, recorder.Count);
        Assert.Equal(1f, recorder.Last.Normal.X, 4);
    }

    [Fact]
    public void NoContact_DoesNothing()
    {
        var ball = new BallSprite3D
        {
            Radius = 0.5f,
            Position = new Vector3(10f, 0f, 0f), // far away, no overlap
            Velocity = new Vector3(-5f, 0f, 0f),
        };
        var barrier = new SphereBarrier3D(Vector3.Zero, 1f);
        var bounce = new BarrierBounce3D();

        bounce.OnHitBarrier(ball, barrier, Ctx);

        // Untouched.
        Assert.Equal(new Vector3(-5f, 0f, 0f), ball.Velocity);
        Assert.Equal(new Vector3(10f, 0f, 0f), ball.Position);
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
