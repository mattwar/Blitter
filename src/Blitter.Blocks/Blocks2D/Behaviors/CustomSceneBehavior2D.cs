namespace Blitter.Blocks2D;

/// <summary>
/// Per-frame callback for a <see cref="CustomSceneBehavior2D"/>.
/// </summary>
public delegate void SceneApplier(Scene2D scene, in UpdateContext context);

/// <summary>
/// A <see cref="Behavior"/> that delegates its per-frame work to a supplied callback.
/// </summary>
public sealed class CustomSceneBehavior2D : Behavior
{
    public CustomSceneBehavior2D()
    {
    }

    public SceneApplier? OnApply { get; set; }

    public override void Apply(in UpdateContext context)
    {
        if (this.Entity is Scene2D scene)
            OnApply?.Invoke(scene, in context);
    }
}
