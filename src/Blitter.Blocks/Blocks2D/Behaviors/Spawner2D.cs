namespace Blitter.Blocks2D;

/// <summary>
/// Raised after a <see cref="Spawner2D"/> adds an entity to its target.
/// </summary>
/// <param name="Source">The behavior instance that raised the event.</param>
/// <param name="Spawned">The entity that was just spawned.</param>
public readonly record struct SpriteSpawned2DEventArgs(Spawner2D Source, IEntity Spawned);

/// <summary>
/// Scene behavior that periodically adds new sprites to a
/// <see cref="PlayField2D"/>. Use for falling debris, enemy waves,
/// pickups, projectiles, particle bursts — any pattern that wants
/// "spawn another one every N seconds" without each game hand-rolling
/// its own timer.
/// </summary>
public abstract class Spawner2D : Behavior, IUpdatable
{
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

    /// <summary>When set, the spawner skips its turn while the playfield already holds this many matching sprites.</summary>
    public int? MaxAlive { get; set; }

    /// <summary>
    /// When set, the spawner stops permanently after producing this many
    /// sprites in total.
    /// </summary>
    public int? MaxTotal { get; set; }

    /// <summary>When true the spawner accumulates time but skips spawning.</summary>
    public bool Paused { get; set; }

    /// <summary>Random source for <see cref="Jitter"/>. Replace for reproducible runs.</summary>
    public Random Random { get; set; } = Random.Shared;

    /// <summary>Total sprites this spawner has produced.</summary>
    public int SpawnedCount { get; private set; }

    /// <summary>Optional handler invoked after a sprite has been added to the target playfield.</summary>
    public IEventHandler<SpriteSpawned2DEventArgs>? Spawned { get; set; }

    // Time remaining before the next spawn attempt.
    private TimeSpan _timeUntilNext;
    // First tick rolls the StartDelay onto the timer.
    private bool _initialized;

    /// <summary>Creates the next entity to spawn. Invoked on the update thread.</summary>
    protected abstract IEntity CreateSprite();

    /// <summary>Resolves the playfield that receives spawned sprites.</summary>
    protected virtual PlayField2D ResolveTarget()
    {
        if (Entity is Scene2D scene)
            return scene.GetLayer<PlayField2D>();

        throw new InvalidOperationException("Spawner2D must be attached to a Scene2D or override ResolveTarget().");
    }

    /// <summary>Returns whether <paramref name="sprite"/> counts toward <see cref="MaxAlive"/>.</summary>
    protected virtual bool CountsTowardMaxAlive(IEntity sprite) => true;

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

    public void Update(in UpdateContext context)
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

        var target = ResolveTarget();

        if (MaxAlive is int cap)
        {
            int alive = target.Sprites.Count(CountsTowardMaxAlive);
            if (alive >= cap)
                return false;
        }

        var sprite = CreateSprite();
        target.AddSprite(sprite);
        SpawnedCount++;
        var args = new SpriteSpawned2DEventArgs(this, sprite);
        Spawned?.OnEvent(in args);
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
