namespace Blitter.Blocks;

/// <summary>
/// A behavior for an element of a <see cref="Scene2D"/>
/// </summary>
public abstract class Behavior2D
{
    /// <summary>
    /// When false the host skips this behavior for the frame.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
