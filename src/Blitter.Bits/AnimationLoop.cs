namespace Blitter.Bits;

/// <summary>
/// How an <see cref="AnimationSequence"/> repeats once it runs past the last frame.
/// </summary>
public enum AnimationLoop
{
    /// <summary>Wrap back to the first frame.</summary>
    Loop,
    /// <summary>Reverse direction at each end, bouncing forever.</summary>
    PingPong,
    /// <summary>Hold on the last frame.</summary>
    Once,
}
