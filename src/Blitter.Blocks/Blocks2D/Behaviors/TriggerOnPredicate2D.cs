namespace Blitter.Blocks2D;

/// <summary>
/// Rising-edge trigger that runs subclass logic once each time its condition transitions from <c>false</c> to <c>true</c>.
/// Useful for "all enemies dead", "player reached the exit", "score ≥ N", or any other one-shot scene gate.
/// </summary>
public abstract class TriggerOnPredicate2D : Behavior, IUpdatable
{
    /// <summary>
    /// When true (default), the trigger may fire each time the
    /// predicate transitions false→true. When false the trigger
    /// disables itself after firing once.
    /// </summary>
    public bool Repeating { get; set; } = true;

    /// <summary>Total fires so far.</summary>
    public int FiredCount { get; private set; }

    // Last observed predicate value; starts false so a predicate that's
    // already true on the first tick fires immediately.
    private bool _last;

    // Set once a non-repeating trigger has fired; further ticks are ignored.
    private bool _spent;

    /// <summary>Returns the trigger condition for the current tick.</summary>
    protected abstract bool IsTriggered(IEntity entity);

    /// <summary>Runs when the trigger condition transitions from false to true.</summary>
    protected abstract void OnTriggered(IEntity entity);

    public void Update(in UpdateContext context)
    {
        if (_spent)
            return;

        if (this.Entity is {} entity)
        {
            var now = IsTriggered(entity);
            if (now && !_last)
            {
                FiredCount++;
                OnTriggered(entity);
                if (!Repeating)
                    _spent = true;
            }
            _last = now;
        }
    }
}
