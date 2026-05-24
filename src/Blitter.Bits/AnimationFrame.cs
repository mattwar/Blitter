namespace Blitter.Bits;

/// <summary>
/// One frame inside an <see cref="AnimationSequence"/>: a texture
/// paired with an optional per-frame flip. Implicitly constructible
/// from a <see cref="Texture2D"/> for the common no-flip case.
/// </summary>
public readonly struct AnimationFrame
{
    /// <summary>Texture drawn for this frame.</summary>
    public Texture2D Texture { get; }

    /// <summary>Mirror applied to the texture when this frame is drawn.</summary>
    public FlipMode Flip { get; }

    public AnimationFrame(Texture2D texture, FlipMode flip = FlipMode.None)
    {
        ArgumentNullException.ThrowIfNull(texture);
        Texture = texture;
        Flip = flip;
    }

    public static implicit operator AnimationFrame(Texture2D texture) =>
        new(texture);
}
