namespace Blitter.Blocks2D;

using System.Numerics;

/// <summary>
/// Shakes a <see cref="Camera2D"/>'s position by an offset driven by a trauma value that decays each tick. 
/// Call <see cref="AddTrauma(float)"/> on impacts / explosions. 
/// Attach to whichever sprite already drives the camera (typically the player) and place <em>after</em>
/// <see cref="CameraFollow2D"/> so the shake offset is added on top of the followed position.
/// </summary>
public sealed class CameraShake2D : SpriteBehavior2D
{
    /// <summary>Camera being jittered. Required.</summary>
    public required Camera2D Camera { get; init; }

    /// <summary>Maximum offset (world units) at full trauma (=1).</summary>
    public float MaxOffset { get; set; } = 16f;

    /// <summary>Trauma units bled off per second.</summary>
    public float Decay { get; set; } = 1.2f;

    /// <summary>Current trauma in <c>[0, 1]</c>. Amplitude is <c>trauma²</c>.</summary>
    public float Trauma { get; private set; }

    /// <summary>Random source. Replace for deterministic playback.</summary>
    public Random Random { get; set; } = Random.Shared;

    // Offset added to Camera.Position on the previous tick. Undone at
    // start of each Apply iff Camera.Position is still what we left it;
    // otherwise treat the current position as the new baseline (an
    // external writer like CameraFollow2D may have moved the camera
    // between our calls).
    private Vector2 _lastOffset;
    private Vector2 _lastWritten;

    /// <summary>Add to current trauma; clamped to <c>[0, 1]</c>.</summary>
    public void AddTrauma(float amount) =>
        Trauma = Math.Clamp(Trauma + amount, 0f, 1f);

    public override void Apply(Sprite2D target, in UpdateContext2D context)
    {
        var baseline = Camera.Position == _lastWritten
            ? Camera.Position - _lastOffset
            : Camera.Position;

        Trauma = Math.Max(0f, Trauma - Decay * (float)context.ElapsedSinceLastUpdate.TotalSeconds);
        var amplitude = MaxOffset * Trauma * Trauma;

        _lastOffset = amplitude > 0f
            ? new Vector2(
                (float)(Random.NextDouble() * 2.0 - 1.0) * amplitude,
                (float)(Random.NextDouble() * 2.0 - 1.0) * amplitude)
            : Vector2.Zero;

        Camera.Position = baseline + _lastOffset;
        _lastWritten = Camera.Position;
    }
}
