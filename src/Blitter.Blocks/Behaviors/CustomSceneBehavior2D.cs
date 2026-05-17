namespace Blitter.Blocks;

/// <summary>
/// Per-frame callback for a <see cref="CustomSceneBehavior2D"/>.
/// </summary>
public delegate void SceneUpdater(Scene2D scene, in UpdateContext2D context);

/// <summary>
/// A <see cref="SceneBehavior2D"/> that delegates its per-frame work to a supplied callback.
/// </summary>
public sealed class CustomSceneBehavior2D : SceneBehavior2D
{
    public CustomSceneBehavior2D()
    {
    }

    public SceneUpdater? OnUpdate { get; set; }

    public override void Update(Scene2D scene, in UpdateContext2D context)
        => OnUpdate?.Invoke(scene, in context);
}
