namespace Blitter.Blocks;

/// <summary>
/// Per-frame callback for a <see cref="CustomSceneBehavior2D"/>.
/// </summary>
public delegate void SceneApplier(Scene2D scene, in UpdateContext2D context);

/// <summary>
/// A <see cref="SceneBehavior2D"/> that delegates its per-frame work to a supplied callback.
/// </summary>
public sealed class CustomSceneBehavior2D : SceneBehavior2D
{
    public CustomSceneBehavior2D()
    {
    }

    public SceneApplier? OnApply { get; set; }

    public override void Apply(Scene2D scene, in UpdateContext2D context)
        => OnApply?.Invoke(scene, in context);
}
