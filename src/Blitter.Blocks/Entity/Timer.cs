namespace Blitter.Blocks;

/// <summary>
/// Raised each time a <see cref="Timer"/> reaches zero.
/// </summary>
/// <param name="Source">The timer that expired.</param>
public readonly record struct TimerExpiredEventArgs(Timer Source)
{
    /// <summary>The entity hosting the timer, if any.</summary>
    public IEntity? Entity => Source.Entity;
}

/// <summary>
/// Counts down each tick; raises a <see cref="TimerExpiredEventArgs"/> event when it reaches zero. 
/// With <see cref="AutoRestart"/> enabled it fires repeatedly at <see cref="Duration"/> intervals — 
/// useful for periodic AI checks, level countdowns, or rate-limited effects.
/// </summary>
public sealed class Timer : Behavior, IUpdatable
{
    /// <summary>Countdown length used on start and (when auto-restarting) after each fire.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Time remaining before the next expiration.</summary>
    public TimeSpan TimeRemaining { get; private set; }

    /// <summary>Total time the timer has been running (excluding paused frames).</summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>When true, the timer restarts automatically after each expiration.</summary>
    public bool AutoRestart { get; set; }

    /// <summary>When true, the timer accumulates time but skips firing.</summary>
    public bool Paused { get; set; }

    /// <summary>Total expirations observed since the timer started.</summary>
    public int FiredCount { get; private set; }

    /// <summary>Optional handler invoked each time the timer expires.</summary>
    public IEventHandler<TimerExpiredEventArgs>? Expired { get; set; }

    private bool _initialized;

    /// <summary>
    /// Reset <see cref="TimeRemaining"/> to <see cref="Duration"/> and resume counting.
    /// </summary>
    public void Restart()
    {
        TimeRemaining = Duration;
        Paused = false;
        _initialized = true;
    }

    public void Update(in UpdateContext context)
    {
        if (!_initialized)
        {
            TimeRemaining = Duration;
            _initialized = true;
        }

        if (Paused || TimeRemaining <= TimeSpan.Zero)
            return;

        TimeRemaining -= context.ElapsedSinceLastUpdate;
        Elapsed += context.ElapsedSinceLastUpdate;

        if (TimeRemaining <= TimeSpan.Zero)
        {
            FiredCount++;
            var args = new TimerExpiredEventArgs(this);
            Expired?.OnEvent(in args);
            // Preserve any overshoot so high-frequency timers don't drift.
            if (AutoRestart && Duration > TimeSpan.Zero)
                TimeRemaining += Duration;
        }
    }
}
