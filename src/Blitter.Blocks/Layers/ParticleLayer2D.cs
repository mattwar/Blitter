using System.Numerics;
using Blitter.Bits;

namespace Blitter.Blocks;

/// <summary>
/// Initial-position shape for a <see cref="ParticleEmitter2D"/>.
/// </summary>
public enum EmitterShape
{
    /// <summary>
    /// All particles spawn at <see cref="ParticleEmitter2D.Position"/>.
    /// </summary>
    Point,
    /// <summary>
    /// Spawn uniformly inside a disk of <see cref="ParticleEmitter2D.Radius"/>.
    /// </summary>
    Disk,
    /// <summary>
    /// Spawn at the emitter position; velocity heading is biased by the cone (see <see cref="ParticleEmitter2D.HeadingDegrees"/> / <see cref="ParticleEmitter2D.SpreadDegrees"/>).
    /// </summary>
    Cone,
    /// <summary>
    /// Spawn uniformly inside an axis-aligned box of <see cref="ParticleEmitter2D.Size"/>.
    /// </summary>
    Box,
}

/// <summary>
/// Look and lifetime knobs sampled per emitted particle. 
/// Ranges are <c>(min, max)</c>; pass equal values for a constant. 
/// When <see cref="Tint"/> is set, it overrides <see cref="StartTint"/>/<see cref="EndTint"/>.
/// </summary>
public struct ParticleStyle
{
    /// <summary>
    /// Lifetime range in seconds (min, max).
    /// </summary>
    public Vector2 LifetimeRange;

    /// <summary>
    /// Initial speed range in world units per second (min, max).
    /// </summary>
    public Vector2 SpeedRange;

    /// <summary>Color at <c>Age == 0</c>.</summary>
    public Color StartTint;

    /// <summary>
    /// Color at <c>Age == Lifetime</c>. Lerped via <c>Age / Lifetime</c>.
    /// </summary>
    public Color EndTint;

    /// <summary>
    /// Optional multi-stop gradient; when set, overrides <see cref="StartTint"/>/<see cref="EndTint"/>.
    /// </summary>
    public Gradient? Tint;

    /// <summary>
    /// A sensible default: white fading to transparent over 1 second, ~60 units/s.
    /// </summary>
    public static ParticleStyle Default => new()
    {
        LifetimeRange = new Vector2(0.8f, 1.2f),
        SpeedRange = new Vector2(40f, 80f),
        StartTint = new Color(255, 255, 255, 255),
        EndTint = new Color(255, 255, 255, 0),
    };
}

/// <summary>
/// Shape + style for a single <see cref="ParticleLayer2D.Emit(in ParticleEmitter2D, int)"/> call.
/// Construct once and reuse — emitters are pure data.
/// </summary>
public struct ParticleEmitter2D
{
    /// <summary>
    /// Center of the emitter in world units.
    /// </summary>
    public Vector2 Position;

    /// <summary>
    /// Spawn shape.
    /// </summary>
    public EmitterShape Shape;

    /// <summary>
    /// Radius for <see cref="EmitterShape.Disk"/>.
    /// </summary>
    public float Radius;

    /// <summary>
    /// Full width/height for <see cref="EmitterShape.Box"/>.
    /// </summary>
    public Vector2 Size;

    /// <summary>
    /// Initial-velocity heading in degrees (0 = up, matching <c>Sprite2D</c>).
    /// For <see cref="EmitterShape.Cone"/> this is the centerline.
    /// </summary>
    public float HeadingDegrees;

    /// <summary>
    /// Full spread angle in degrees around <see cref="HeadingDegrees"/>.
    /// 360 (the default) is omnidirectional; 0 fires in a perfect line.
    /// </summary>
    public float SpreadDegrees;

    /// <summary>
    /// Per-particle style sampled on emit.
    /// </summary>
    public ParticleStyle Style;

    /// <summary>
    /// An omnidirectional point emitter at the origin with default style.
    /// </summary>
    public static ParticleEmitter2D Default => new()
    {
        Shape = EmitterShape.Point,
        SpreadDegrees = 360f,
        Style = ParticleStyle.Default,
    };
}

/// <summary>
/// A fixed-capacity CPU particle pool drawn as single-pixel points.
/// Pure storage + renderer — callers <see cref="Emit(Vector2, int, in ParticleStyle)"/>
/// from their own hit / spawn callbacks. Recycles the oldest live
/// particle when full.
/// </summary>
public sealed class ParticleLayer2D : Layer2D
{
    // Ring buffer in struct-of-arrays form. Iterating arrays beats
    // chasing a list of objects for both cache and draw-batching.
    private readonly Vector2[] _position;
    private readonly Vector2[] _velocity;
    private readonly float[] _age;
    private readonly float[] _lifetime;
    private readonly Color[] _startTint;
    private readonly Color[] _endTint;
    private readonly Gradient?[] _gradient;
    private readonly Random _rng;

    private int _writeIndex;
    private int _liveCount;

    /// <summary>
    /// Maximum number of simultaneously live particles.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Currently live (Age &lt; Lifetime) particles.
    /// </summary>
    public int LiveCount => _liveCount;

    /// <summary>
    /// Constant force applied each frame to every live particle (world units / sec²). Defaults to zero (no gravity).
    /// </summary>
    public Vector2 Acceleration { get; set; }

    /// <summary>
    /// Exponential damping per second. <c>0</c> = none; <c>1</c> ≈ velocity drops to ~37% per second.
    /// </summary>
    public float Drag { get; set; }

    /// <summary>
    /// Creates a particle pool with fixed capacity. <paramref name="seed"/> seeds the per-emit RNG for deterministic tests.
    /// </summary>
    public ParticleLayer2D(int capacity, int seed = 0)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _position = new Vector2[capacity];
        _velocity = new Vector2[capacity];
        _age = new float[capacity];
        _lifetime = new float[capacity];
        _startTint = new Color[capacity];
        _endTint = new Color[capacity];
        _gradient = new Gradient?[capacity];
        // Mark every slot dead initially.
        for (int i = 0; i < capacity; i++)
            _lifetime[i] = -1f;
        _rng = new Random(seed);
    }

    /// <summary>
    /// Kills every live particle.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < Capacity; i++)
            _lifetime[i] = -1f;
        _liveCount = 0;
        _writeIndex = 0;
    }

    /// <summary>
    /// Emit <paramref name="count"/> particles at <paramref name="position"/> with the given <paramref name="style"/>.
    /// </summary>
    public void Emit(Vector2 position, int count, in ParticleStyle style)
    {
        var emitter = new ParticleEmitter2D
        {
            Position = position,
            Shape = EmitterShape.Point,
            SpreadDegrees = 360f,
            Style = style,
        };
        Emit(emitter, count);
    }

    /// <summary>
    /// Emit <paramref name="count"/> particles using <paramref name="emitter"/>.
    /// </summary>
    public void Emit(in ParticleEmitter2D emitter, int count)
    {
        if (count <= 0)
            return;

        var style = emitter.Style;
        for (int n = 0; n < count; n++)
        {
            var slot = _writeIndex;
            _writeIndex = (_writeIndex + 1) % Capacity;
            // Count only newly-allocated slots; overwritten-live slots stay net-flat.
            if (_lifetime[slot] < 0f || _age[slot] >= _lifetime[slot])
            {
                if (_liveCount < Capacity)
                    _liveCount++;
            }

            var (pos, headingDeg) = SamplePositionAndHeading(emitter);
            var speed = Lerp(style.SpeedRange.X, style.SpeedRange.Y, NextFloat());
            var headingRad = headingDeg * (MathF.PI / 180f);
            // Heading convention: 0 = up (-Y), matches Sprite2D.GetVelocity.
            var vel = new Vector2(MathF.Sin(headingRad), -MathF.Cos(headingRad)) * speed;
            var life = Lerp(style.LifetimeRange.X, style.LifetimeRange.Y, NextFloat());

            _position[slot] = pos;
            _velocity[slot] = vel;
            _age[slot] = 0f;
            _lifetime[slot] = life > 0f ? life : 0.0001f;
            _startTint[slot] = style.StartTint;
            _endTint[slot] = style.EndTint;
            _gradient[slot] = style.Tint;
        }
    }

    public override void Update(in UpdateContext2D context)
    {
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f)
            return;

        var dragFactor = Drag > 0f ? MathF.Exp(-Drag * dt) : 1f;
        var accelStep = Acceleration * dt;

        for (int i = 0; i < Capacity; i++)
        {
            var life = _lifetime[i];
            if (life < 0f)
                continue;

            var age = _age[i] + dt;
            if (age >= life)
            {
                // First frame this slot becomes dead.
                _lifetime[i] = -1f;
                _gradient[i] = null;
                _liveCount--;
                continue;
            }

            var vel = _velocity[i] + accelStep;
            if (dragFactor != 1f)
                vel *= dragFactor;

            _velocity[i] = vel;
            _position[i] += vel * dt;
            _age[i] = age;
        }
    }

    protected override void DrawContent(Renderer2D renderer)
    {
        if (_liveCount == 0)
            return;

        using var _ = renderer.PushState();
        for (int i = 0; i < Capacity; i++)
        {
            var life = _lifetime[i];
            if (life < 0f)
                continue;
            var t = _age[i] / life;
            var color = _gradient[i] is { } g
                ? g.Sample(t)
                : Color.Lerp(_startTint[i], _endTint[i], t);
            renderer.DrawColor = color;
            renderer.DrawPoint(_position[i].X, _position[i].Y);
        }
    }

    private (Vector2 Position, float HeadingDegrees) SamplePositionAndHeading(in ParticleEmitter2D emitter)
    {
        Vector2 pos = emitter.Position;
        float headingDeg;

        switch (emitter.Shape)
        {
            case EmitterShape.Disk:
                {
                    // Uniform inside a disk: r = R*sqrt(u), theta = 2*pi*v.
                    var r = emitter.Radius * MathF.Sqrt(NextFloat());
                    var theta = NextFloat() * MathF.Tau;
                    pos += new Vector2(MathF.Cos(theta) * r, MathF.Sin(theta) * r);
                    headingDeg = SampleHeading(emitter);
                    break;
                }
            case EmitterShape.Box:
                {
                    var hx = emitter.Size.X * 0.5f;
                    var hy = emitter.Size.Y * 0.5f;
                    pos += new Vector2((NextFloat() * 2f - 1f) * hx, (NextFloat() * 2f - 1f) * hy);
                    headingDeg = SampleHeading(emitter);
                    break;
                }
            case EmitterShape.Cone:
            case EmitterShape.Point:
            default:
                headingDeg = SampleHeading(emitter);
                break;
        }
        return (pos, headingDeg);
    }

    private float SampleHeading(in ParticleEmitter2D emitter)
    {
        // Symmetric spread around HeadingDegrees. Spread 360 = omni.
        var spread = emitter.SpreadDegrees;
        if (spread <= 0f)
            return emitter.HeadingDegrees;
        var jitter = (NextFloat() - 0.5f) * spread;
        return emitter.HeadingDegrees + jitter;
    }

    private float NextFloat() => (float)_rng.NextDouble();

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    // ---- Test hooks (InternalsVisibleTo Blitter.Blocks.Tests). ----

    internal IReadOnlyList<Vector2> LivePositions
    {
        get
        {
            var list = new List<Vector2>(_liveCount);
            for (int i = 0; i < Capacity; i++)
                if (_lifetime[i] >= 0f)
                    list.Add(_position[i]);
            return list;
        }
    }

    internal IReadOnlyList<Color> LiveColors
    {
        get
        {
            var list = new List<Color>(_liveCount);
            for (int i = 0; i < Capacity; i++)
            {
                var life = _lifetime[i];
                if (life < 0f)
                    continue;
                var t = _age[i] / life;
                list.Add(_gradient[i] is { } g
                    ? g.Sample(t)
                    : Color.Lerp(_startTint[i], _endTint[i], t));
            }
            return list;
        }
    }
}
