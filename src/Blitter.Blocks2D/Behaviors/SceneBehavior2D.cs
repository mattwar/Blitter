namespace Blitter.Blocks2D;
using Bits;

/// <summary>
/// Scene-wide logic that runs once per tick, before the scene's
/// layers update. Reacts to and orchestrates what's going on across
/// layers (e.g. monitor input, trigger sounds/HUD changes, call
/// <see cref="Scene2D.Exit"/>). Behaviors don't render — anything
/// visual goes in a <see cref="Layer2D"/>.
/// </summary>
public abstract class SceneBehavior2D : Behavior2D
{
}
