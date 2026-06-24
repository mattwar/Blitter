using System.Numerics;

namespace Blitter.Blocks2D;


/// <summary>
/// Shakes a sprite's <see cref="Sprite2D.Center"/> by an offset driven by a trauma value that decays each tick. 
/// Place this behavior <em>after</em> <see cref="Motion2D"/> so motion
/// integration runs against the unshaken position.
/// </summary>
public sealed class Shake2D : Behavior, IUpdatable
{
    /// <summary>Maximum offset magnitude (pixels) at full trauma (=1).</summary>
    public float MaxOffset { get; set; } = 8f;

    /// <summary>Trauma units bled off per second. Higher = shorter shake.</summary>
    public float Decay { get; set; } = 1.2f;

    /// <summary>Current trauma in <c>[0, 1]</c>. Display amplitude is <c>trauma²</c>.</summary>
    public float Trauma { get; private set; }

    /// <summary>Random source. Replace for deterministic playback.</summary>
    public Random Random { get; set; } = Random.Shared;

    // Offset added to Center on the previous tick; undone at the start
    // of each Apply so subsequent behaviors / Motion2D see the true center.
    private Vector2 _lastOffset;

    /// <summary>Add to current trauma; clamped to <c>[0, 1]</c>.</summary>
    public void AddTrauma(float amount) =>
        Trauma = Math.Clamp(Trauma + amount, 0f, 1f);


    private Transform2D _transform = null!;

    protected override void OnAttach(IEntity entity)
    {
        _transform = entity.GetOrAddTrait<Transform2D>();
    }

    public void Update(in UpdateContext context)
    {
        // Roll back last frame's shake before recomputing.
        _transform.Position -= _lastOffset;

        Trauma = Math.Max(0f, Trauma - Decay * (float)context.ElapsedSinceLastUpdate.TotalSeconds);
        var amplitude = MaxOffset * Trauma * Trauma;

        _lastOffset = amplitude > 0f
            ? new Vector2(
                (float)(Random.NextDouble() * 2.0 - 1.0) * amplitude,
                (float)(Random.NextDouble() * 2.0 - 1.0) * amplitude)
            : Vector2.Zero;

        _transform.Position += _lastOffset;
    }
}
