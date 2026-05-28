namespace Blitter;

/// <summary>
/// Lifecycle state of a scene's run loop. Shared between
/// <c>Blitter.Blocks2D.Scene2D</c> and <c>Blitter.Blocks3D.Scene3D</c>.
/// </summary>
public enum RunState
{
    /// <summary>The scene is running normally.</summary>
    Running,
    /// <summary>Exit has been requested; the scene keeps advancing until all exit conditions are met.</summary>
    Exiting,
    /// <summary>All exit conditions have been met; the run loop has stopped (or will on its next check).</summary>
    Exited,
}
