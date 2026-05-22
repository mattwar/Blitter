using System.Collections.Immutable;

namespace Blitter.Bits;

/// <summary>
/// A named sequence of atlas frame indices played at a fixed cadence with a chosen loop behavior. 
/// Immutable; safe to share across <see cref="AnimationAtlas"/> consumers.
/// </summary>
public sealed class AnimationSequence
{
    public AnimationSequence(
        string name,
        ImmutableArray<int> frames,
        TimeSpan frameDuration,
        AnimationLoop loop = AnimationLoop.Loop)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (frames.IsDefaultOrEmpty)
            throw new ArgumentException("At least one frame is required.", nameof(frames));
        if (frameDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(frameDuration));

        Name = name;
        Frames = frames;
        FrameDuration = frameDuration;
        Loop = loop;
    }

    /// <summary>Identifier used to select this sequence as a visual's state.</summary>
    public string Name { get; }

    /// <summary>Atlas region indices, in playback order.</summary>
    public ImmutableArray<int> Frames { get; }

    /// <summary>Time each frame is held.</summary>
    public TimeSpan FrameDuration { get; }

    /// <summary>Behavior at the end of the sequence.</summary>
    public AnimationLoop Loop { get; }

    /// <summary>Number of frames in the sequence.</summary>
    public int FrameCount => Frames.Length;

    /// <summary>
    /// Atlas frame index drawn after <paramref name="elapsed"/> time
    /// in this sequence (measured from when the sequence started).
    /// </summary>
    public int FrameIndexAt(TimeSpan elapsed)
    {
        int n = Frames.Length;
        if (n == 1) return Frames[0];

        var step = (long)Math.Floor(elapsed.TotalSeconds / FrameDuration.TotalSeconds);
        long idx = Loop switch
        {
            AnimationLoop.Loop => Mod(step, n),
            AnimationLoop.PingPong => PingPong(step, n),
            AnimationLoop.Once => step < 0 ? 0 : Math.Min(step, n - 1),
            _ => 0,
        };
        return Frames[(int)idx];

        static long Mod(long a, int m)
        {
            var r = a % m;
            return r < 0 ? r + m : r;
        }

        static long PingPong(long step, int n)
        {
            if (n <= 1) return 0;
            int period = 2 * (n - 1);
            var s = Mod(step, period);
            return s < n ? s : period - s;
        }
    }

    /// <summary>
    /// True for <see cref="AnimationLoop.Once"/> sequences after they
    /// have reached the last frame. Always false for looping modes.
    /// </summary>
    public bool IsAtEnd(TimeSpan elapsed)
    {
        if (Loop != AnimationLoop.Once) return false;
        var step = (long)Math.Floor(elapsed.TotalSeconds / FrameDuration.TotalSeconds);
        return step >= Frames.Length - 1;
    }
}
