namespace Blitter.Blocks2D;
using Bits;

/// <summary>
/// Scene behavior that periodically adds new sprites to a
/// <see cref="PlayField2D"/>. Use for falling debris, enemy waves,
/// pickups, projectiles, particle bursts — any pattern that wants
/// "spawn another one every N seconds" without each game hand-rolling
/// its own timer.
/// </summary>
public sealed class Spawner2D : Behavior
{
    /// <summary>Playfield that receives spawned sprites.</summary>
    public required PlayField2D Target { get; init; }

    /// <summary>Produces the next sprite to spawn. Invoked on the update thread.</summary>
    public required Func<Sprite2D> Factory { get; init; }

    /// <summary>Average time between spawns.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Random additive jitter applied to each interval, uniformly in
    /// <c>[-Jitter, +Jitter]</c>. Breaks the visible heartbeat of a
    /// fixed-cadence spawner.
    /// </summary>
    public TimeSpan Jitter { get; set; } = TimeSpan.Zero;

    /// <summary>Wait this long after the spawner starts before the first spawn.</summary>
    public TimeSpan StartDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// When set, the spawner skips its turn while the playfield already
    /// holds this many matching sprites (counted via <see cref="Filter"/>
    /// when supplied, otherwise the total sprite count).
    /// </summary>
    public int? MaxAlive { get; set; }

    /// <summary>
    /// When set, the spawner stops permanently after producing this many
    /// sprites in total.
    /// </summary>
    public int? MaxTotal { get; set; }

    /// <summary>
    /// Optional predicate that selects which sprites count toward
    /// <see cref="MaxAlive"/>. Lets one spawner ignore sprites managed
    /// by another (e.g. only count <c>Enemy</c> sprites when sharing
    /// the playfield with bullets and pickups).
    /// </summary>
    public Func<Sprite2D, bool>? Filter { get; set; }

    /// <summary>When true the spawner accumulates time but skips spawning.</summary>
    public bool Paused { get; set; }

    /// <summary>Random source for <see cref="Jitter"/>. Replace for reproducible runs.</summary>
    public Random Random { get; set; } = Random.Shared;

    /// <summary>Total sprites this spawner has produced.</summary>
    public int SpawnedCount { get; private set; }

    /// <summary>
    /// Raised after a spawned sprite has been added to <see cref="Target"/>.
    /// Useful for logging, attaching extra behaviors, or anchoring
    /// position relative to another sprite.
    /// </summary>
    public event Action<Sprite2D>? Spawned;

    // Time remaining before the next spawn attempt.
    private TimeSpan _timeUntilNext;
    // First tick rolls the StartDelay onto the timer.
    private bool _initialized;

    /// <summary>
    /// Spawn <paramref name="count"/> sprites immediately, independent
    /// of the interval timer. Honors <see cref="MaxAlive"/> and
    /// <see cref="MaxTotal"/>. Returns the number actually spawned.
    /// </summary>
    public int Burst(int count)
    {
        if (count <= 0)
            return 0;
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            if (!TrySpawn())
                break;
            spawned++;
        }
        return spawned;
    }

    public override void Apply(in UpdateContext context)
    {
        if (!_initialized)
        {
            _timeUntilNext = StartDelay + RollJitter();
            _initialized = true;
        }

        if (Paused)
            return;

        _timeUntilNext -= context.ElapsedSinceLastUpdate;
        if (_timeUntilNext > TimeSpan.Zero)
            return;

        if (TrySpawn())
        {
            _timeUntilNext = Interval + RollJitter();
        }
        else
        {
            // Cap reached — re-check next tick without letting the
            // timer accumulate a debt that would burst once the cap
            // drops below the limit.
            _timeUntilNext = Interval;
        }
    }

    private bool TrySpawn()
    {
        if (MaxTotal is int total && SpawnedCount >= total)
            return false;

        if (MaxAlive is int cap)
        {
            int alive = Filter is { } f
                ? Target.Sprites.Count(f)
                : Target.Sprites.Count;
            if (alive >= cap)
                return false;
        }

        var sprite = Factory();
        Target.AddSprite(sprite);
        SpawnedCount++;
        Spawned?.Invoke(sprite);
        return true;
    }

    private TimeSpan RollJitter()
    {
        if (Jitter <= TimeSpan.Zero)
            return TimeSpan.Zero;
        var j = Jitter.TotalSeconds;
        var r = (Random.NextDouble() * 2.0 - 1.0) * j;
        return TimeSpan.FromSeconds(r);
    }
}
