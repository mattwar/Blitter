using System.Numerics;

namespace Blitter.Bits;

/// <summary>How an <see cref="AnimatedVisual2D"/> repeats once it
/// runs past the last frame.</summary>
public enum AnimationLoop
{
    /// <summary>Wrap back to the first frame.</summary>
    Loop,
    /// <summary>Reverse direction at each end, bouncing forever.</summary>
    PingPong,
    /// <summary>Hold on the last frame.</summary>
    Once,
}

/// <summary>
/// A <see cref="Visual2D"/> that cycles through frames of an
/// <see cref="Atlas"/>. Playback is driven by the <c>elapsed</c>
/// argument <see cref="Visual2D.Draw"/> receives, so a single
/// instance can be shared across many hosts — each host's elapsed
/// time is its own timeline.
/// </summary>
public sealed class AnimatedVisual2D : Visual2D
{
    private readonly Atlas _atlas;
    private readonly int[] _frames;
    private BoundingCircle? _boundary;

    public AnimatedVisual2D(
        Atlas atlas,
        TimeSpan frameDuration,
        AnimationLoop loop = AnimationLoop.Loop,
        TimeSpan offset = default,
        ReadOnlySpan<int> frames = default)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        if (frameDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(frameDuration));

        _atlas = atlas;
        _frames = frames.IsEmpty
            ? BuildDefaultFrames(atlas.Count)
            : frames.ToArray();
        if (_frames.Length == 0)
            throw new ArgumentException("At least one frame is required.", nameof(frames));
        for (int i = 0; i < _frames.Length; i++)
        {
            if ((uint)_frames[i] >= (uint)atlas.Count)
                throw new ArgumentOutOfRangeException(nameof(frames),
                    $"Frame index {_frames[i]} is outside the atlas range [0, {atlas.Count}).");
        }

        FrameDuration = frameDuration;
        Loop = loop;
        Offset = offset;
    }

    /// <summary>Atlas providing the source regions.</summary>
    public Atlas Atlas => _atlas;

    /// <summary>Atlas region indices, in playback order.</summary>
    public ReadOnlySpan<int> Frames => _frames;

    /// <summary>Time each frame is held.</summary>
    public TimeSpan FrameDuration { get; }

    /// <summary>Loop behavior at the end of the sequence.</summary>
    public AnimationLoop Loop { get; }

    /// <summary>Phase offset added to <c>elapsed</c> before picking a frame.
    /// Use <see cref="WithOffset"/> to clone an instance out of phase.</summary>
    public TimeSpan Offset { get; }

    /// <summary>Number of frames in the sequence.</summary>
    public int FrameCount => _frames.Length;

    /// <summary>Returns a copy of this visual with a different phase
    /// <paramref name="offset"/>; useful for desyncing hosts that
    /// share an animation.</summary>
    public AnimatedVisual2D WithOffset(TimeSpan offset) =>
        new(_atlas, FrameDuration, Loop, offset, _frames);

    /// <summary>Returns a copy of this visual with a different
    /// <paramref name="frameDuration"/>.</summary>
    public AnimatedVisual2D WithFrameDuration(TimeSpan frameDuration) =>
        new(_atlas, frameDuration, Loop, Offset, _frames);

    /// <summary>Returns a copy of this visual with a different
    /// <paramref name="loop"/> behavior.</summary>
    public AnimatedVisual2D WithLoop(AnimationLoop loop) =>
        new(_atlas, FrameDuration, loop, Offset, _frames);

    /// <summary>Atlas region index that should be drawn for the given
    /// elapsed time.</summary>
    public int FrameIndexAt(TimeSpan elapsed)
    {
        int n = _frames.Length;
        if (n == 1) return _frames[0];

        var step = (long)Math.Floor((elapsed + Offset).TotalSeconds / FrameDuration.TotalSeconds);
        long idx = Loop switch
        {
            AnimationLoop.Loop => Mod(step, n),
            AnimationLoop.PingPong => PingPong(step, n),
            AnimationLoop.Once => step < 0 ? 0 : Math.Min(step, n - 1),
            _ => 0,
        };
        return _frames[idx];

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

    /// <summary>Atlas region for the current frame at the given time.</summary>
    public Rect RegionAt(TimeSpan elapsed) => _atlas[FrameIndexAt(elapsed)];

    /// <summary>Bounding circle covering the largest frame in the sequence.</summary>
    public override BoundingCircle Boundary => _boundary ??= ComputeBoundary();

    private BoundingCircle ComputeBoundary()
    {
        float maxR2 = 0f;
        for (int i = 0; i < _frames.Length; i++)
        {
            var r = _atlas[_frames[i]];
            var hw = r.Width / 2f;
            var hh = r.Height / 2f;
            var r2 = hw * hw + hh * hh;
            if (r2 > maxR2) maxR2 = r2;
        }
        return new BoundingCircle(Vector2.Zero, MathF.Sqrt(maxR2));
    }

    public override void Draw(Renderer2D renderer, in Pose2D pose, Color tint, TimeSpan elapsed)
    {
        var source = RegionAt(elapsed);
        var scaledW = source.Width * pose.Scale;
        var scaledH = source.Height * pose.Scale;
        var dest = new Rect(
            pose.Position.X - scaledW / 2f,
            pose.Position.Y - scaledH / 2f,
            scaledW,
            scaledH);
        bool tinted = tint != Color.White;

        if (pose.Rotation != 0f || pose.Flipped != FlipMode.None)
        {
            var rc = new Vector2(scaledW / 2f, scaledH / 2f);
            if (tinted)
                renderer.DrawImageRotated(_atlas.Image, source, dest, pose.Rotation, rc, pose.Flipped, tint);
            else
                renderer.DrawImageRotated(_atlas.Image, source, dest, pose.Rotation, rc, pose.Flipped);
        }
        else
        {
            if (tinted)
                renderer.DrawImage(_atlas.Image, source, dest, tint);
            else
                renderer.DrawImage(_atlas.Image, source, dest);
        }
    }

    private static int[] BuildDefaultFrames(int count)
    {
        var a = new int[count];
        for (int i = 0; i < count; i++) a[i] = i;
        return a;
    }
}
