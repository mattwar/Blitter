namespace Blitter.Blocks3D;
using Bits;

/// <summary>
/// A behavior for an element of a <see cref="Scene3D"/>.
/// </summary>
public abstract class Behavior3D : Behavior
{
    /// <summary>When false the host skips this behavior for the frame.</summary>
    public bool Enabled { get; set; } = true;
}
