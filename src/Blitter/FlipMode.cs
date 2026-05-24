namespace Blitter;

/// <summary>
/// How to flip a sprite or image when rendering. Horizontal and
/// Vertical compose via bitwise OR into <see cref="Both"/>; the
/// four values form a Klein four-group under XOR.
/// </summary>
[Flags]
public enum FlipMode
{
    None       = SDL.FlipMode.None,
    Horizontal = SDL.FlipMode.Horizontal,
    Vertical   = SDL.FlipMode.Vertical,
    Both       = Horizontal | Vertical,
}
