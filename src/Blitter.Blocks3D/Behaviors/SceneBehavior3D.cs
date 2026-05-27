namespace Blitter.Blocks3D;

/// <summary>
/// Scene-wide logic that runs once per frame, before the scene's layers
/// update. Reacts to and orchestrates what's going on across layers
/// (e.g. monitor input, trigger sounds/HUD changes, call
/// <see cref="Scene3D.Exit"/>). Behaviors don't render — anything
/// visual goes in a <see cref="Layer3D"/>.
/// </summary>
public abstract class SceneBehavior3D
{
    /// <summary>When false the scene skips this behavior.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Apply this behavior to <paramref name="scene"/> for one frame.</summary>
    public abstract void Apply(Scene3D scene, in UpdateContext3D context);
}
