namespace Blitter.Blocks2D;
using Bits;

/// <summary>
/// Rising-edge trigger: invokes <see cref="Action"/> once each time <see cref="Predicate"/> transitions from <c>false</c> to <c>true</c>.
/// Useful for "all enemies dead", "player reached the exit", "score ≥ N", or any other one-shot scene gate.
/// </summary>
public sealed class TriggerOnPredicate2D : Behavior
{
    /// <summary>Condition evaluated each tick.</summary>
    public required Func<IEntity, bool> Predicate { get; init; }

    /// <summary>Invoked on each rising edge of <see cref="Predicate"/>.</summary>
    public required Action<IEntity> Action { get; init; }

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

    public override void Apply(in UpdateContext context)
    {
        if (_spent)
            return;

        if (this.Entity is {} entity)
        {
            var now = Predicate(entity);
            if (now && !_last)
            {
                FiredCount++;
                Action(entity);
                if (!Repeating)
                    _spent = true;
            }
            _last = now;
        }
    }
}
