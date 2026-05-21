namespace Blitter.Blocks;

/// <summary>
/// Rising-edge trigger: invokes <see cref="Action"/> once each time <see cref="Predicate"/> transitions from <c>false</c> to <c>true</c>.
/// Useful for "all enemies dead", "player reached the exit", "score ≥ N", or any other one-shot scene gate.
/// </summary>
public sealed class TriggerOnPredicate2D : SceneBehavior2D
{
    /// <summary>Condition evaluated each tick.</summary>
    public required Func<Scene2D, bool> Predicate { get; init; }

    /// <summary>Invoked on each rising edge of <see cref="Predicate"/>.</summary>
    public required Action<Scene2D> Action { get; init; }

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

    public override void Apply(Scene2D scene, in UpdateContext2D context)
    {
        var now = Predicate(scene);
        if (now && !_last)
        {
            FiredCount++;
            Action(scene);
            if (!Repeating)
                Enabled = false;
        }
        _last = now;
    }
}
