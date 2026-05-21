namespace Blitter.Blocks.Tests;

using System.Numerics;
using Blitter.Bits;

public class ParticleLayer2DTests
{
    private static UpdateContext2D Context(double seconds) =>
        new() { ElapsedSinceLastUpdate = TimeSpan.FromSeconds(seconds) };

    private static ParticleStyle FixedStyle(float lifetime, float speed) => new()
    {
        LifetimeRange = new Vector2(lifetime, lifetime),
        SpeedRange = new Vector2(speed, speed),
        StartTint = new Color(255, 255, 255, 255),
        EndTint = new Color(255, 255, 255, 0),
    };

    [Fact]
    public void Emit_AddsLiveParticles()
    {
        var layer = new ParticleLayer2D(capacity: 16);
        layer.Emit(new Vector2(10, 20), 5, FixedStyle(1f, 0f));
        Assert.Equal(5, layer.LiveCount);
    }

    [Fact]
    public void Particle_DiesAfterLifetime()
    {
        var layer = new ParticleLayer2D(capacity: 8);
        layer.Emit(Vector2.Zero, 3, FixedStyle(0.5f, 0f));
        Assert.Equal(3, layer.LiveCount);

        layer.Update(Context(0.6));
        Assert.Equal(0, layer.LiveCount);
    }

    [Fact]
    public void Acceleration_AddsToVelocity()
    {
        // Zero initial speed + gravity = particle falls.
        var layer = new ParticleLayer2D(capacity: 4)
        {
            Acceleration = new Vector2(0f, 100f),
        };
        layer.Emit(Vector2.Zero, 1, FixedStyle(10f, 0f));
        layer.Update(Context(1.0));

        var pos = layer.LivePositions.Single();
        // After 1s under 100 units/s² with no initial velocity: v=100, pos=v*dt=100.
        Assert.Equal(100f, pos.Y, precision: 3);
    }

    [Fact]
    public void Drag_DampsVelocity()
    {
        var layer = new ParticleLayer2D(capacity: 4) { Drag = 1f };
        // Speed 100 along heading 90 (right): velocity = (100, 0).
        layer.Emit(new ParticleEmitter2D
        {
            Position = Vector2.Zero,
            Shape = EmitterShape.Point,
            HeadingDegrees = 90f,
            SpreadDegrees = 0f,
            Style = FixedStyle(10f, 100f),
        }, 1);

        layer.Update(Context(1.0));
        var pos = layer.LivePositions.Single();
        // Drag=1, dt=1: vel *= exp(-1) ≈ 0.368; pos = 0.368 * 100 ≈ 36.8.
        Assert.InRange(pos.X, 30f, 45f);
        Assert.Equal(0f, pos.Y, precision: 3);
    }

    [Fact]
    public void Overflow_RecyclesOldest()
    {
        var layer = new ParticleLayer2D(capacity: 4);
        var style = FixedStyle(10f, 0f);
        for (int i = 0; i < 4; i++)
            layer.Emit(new Vector2(i, 0), 1, style);
        Assert.Equal(4, layer.LiveCount);

        // Next 2 emits should overwrite slots 0 and 1; total still 4.
        layer.Emit(new Vector2(100, 0), 1, style);
        layer.Emit(new Vector2(101, 0), 1, style);
        Assert.Equal(4, layer.LiveCount);

        var xs = layer.LivePositions.Select(p => p.X).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 2f, 3f, 100f, 101f }, xs);
    }

    [Fact]
    public void Gradient_OverridesStartEndTint()
    {
        var layer = new ParticleLayer2D(capacity: 2);
        var red = new Color(255, 0, 0, 255);
        var blue = new Color(0, 0, 255, 255);
        var style = new ParticleStyle
        {
            LifetimeRange = new Vector2(1f, 1f),
            SpeedRange = Vector2.Zero,
            StartTint = new Color(0, 255, 0, 255), // green — should be ignored
            EndTint = new Color(0, 255, 0, 255),
            Tint = Gradient.FromColors(red, blue),
        };
        layer.Emit(Vector2.Zero, 1, style);

        // At t=0 the gradient samples to red, not the green Start/EndTint.
        var c0 = layer.LiveColors.Single();
        Assert.Equal(red.R, c0.R);
        Assert.Equal(red.G, c0.G);
        Assert.Equal(red.B, c0.B);

        layer.Update(Context(0.5));
        var c1 = layer.LiveColors.Single();
        // Midway sampled color should not be the green Start/End tint.
        Assert.NotEqual<byte>(255, c1.G);
    }

    [Fact]
    public void Clear_DropsAllParticles()
    {
        var layer = new ParticleLayer2D(capacity: 8);
        layer.Emit(Vector2.Zero, 5, FixedStyle(10f, 0f));
        Assert.Equal(5, layer.LiveCount);
        layer.Clear();
        Assert.Equal(0, layer.LiveCount);
    }

    [Fact]
    public void Emit_ConeZeroSpread_ProducesExactHeading()
    {
        // Cone with 0 spread and heading 90 (right) → velocity exactly (+x).
        var layer = new ParticleLayer2D(capacity: 4);
        layer.Emit(new ParticleEmitter2D
        {
            Position = Vector2.Zero,
            Shape = EmitterShape.Cone,
            HeadingDegrees = 90f,
            SpreadDegrees = 0f,
            Style = FixedStyle(10f, 100f),
        }, 1);

        layer.Update(Context(0.5));
        var pos = layer.LivePositions.Single();
        Assert.Equal(50f, pos.X, precision: 3);
        Assert.Equal(0f, pos.Y, precision: 3);
    }
}
