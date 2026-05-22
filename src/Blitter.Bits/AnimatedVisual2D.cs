using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// A <see cref="Visual2D"/> that plays a named <see cref="AnimationSequence"/>
/// from an <see cref="AnimationAtlas"/>. Set <see cref="Visual2D.State"/>
/// to switch sequences; the new sequence starts from its first frame.
/// </summary>
public sealed class AnimatedVisual2D : Visual2D
{
    private readonly AnimationAtlas _atlas;
    private BoundingCircle? _boundary;

    // Local playback clock: the elapsed value that the current sequence treats as
    // "time zero". On a State change we mark the clock as pending-reset; the next
    // Draw stamps the base so the new sequence starts from frame 0.
    private string _state;
    private AnimationSequence _current;
    private TimeSpan _sequenceStartElapsed;
    private bool _resetPending;

    public AnimatedVisual2D(AnimationAtlas atlas, TimeSpan offset = default)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        _atlas = atlas;
        _state = atlas.DefaultState;
        _current = atlas[_state];
        Offset = offset;
    }

    private AnimatedVisual2D(AnimationAtlas atlas, string state, TimeSpan offset)
    {
        _atlas = atlas;
        _state = state;
        _current = atlas[state];
        Offset = offset;
    }

    /// <summary>Atlas + sequences this visual draws from.</summary>
    public AnimationAtlas Atlas => _atlas;

    /// <inheritdoc/>
    public override string State
    {
        get => _state;
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            if (value == _state) 
                return;
            if (!_atlas.TryGet(value, out var seq))
                throw new ArgumentException($"Unknown state '{value}'.", nameof(value));
            _state = value;
            _current = seq;
            _resetPending = true;
        }
    }

    /// <inheritdoc/>
    public override IReadOnlyList<string> States => 
        _atlas.States;

    /// <summary>Sequence currently selected by <see cref="State"/>.</summary>
    public AnimationSequence CurrentSequence => 
        _current;

    /// <summary>
    /// Phase offset added to local sequence time before picking a frame.
    /// Useful for starting a second host's animation out of phase.
    /// </summary>
    public TimeSpan Offset { get; }

    /// <summary>
    /// Returns a copy of this visual with a different phase
    /// <paramref name="offset"/>.
    /// </summary>
    public AnimatedVisual2D WithOffset(TimeSpan offset) =>
        new(_atlas, _state, offset);

    /// <summary>
    /// Atlas region index that should be drawn for the given host
    /// <paramref name="elapsed"/> time in the current sequence.
    /// </summary>
    public int FrameIndexAt(TimeSpan elapsed) =>
        _current.FrameIndexAt(LocalTime(elapsed));

    /// <summary>Atlas region for the current frame at the given time.</summary>
    public Rect RegionAt(TimeSpan elapsed) =>
        _atlas.Atlas[FrameIndexAt(elapsed)];

    /// <summary>True if the current sequence is <see cref="AnimationLoop.Once"/>
    /// and has reached its last frame.</summary>
    public bool IsAtEnd(TimeSpan elapsed) =>
        _current.IsAtEnd(LocalTime(elapsed));

    /// <summary>
    /// Bounding circle covering the largest frame across every sequence
    /// in the atlas.
    /// </summary>
    public override BoundingCircle Boundary => _boundary ??= ComputeBoundary();

    private BoundingCircle ComputeBoundary()
    {
        float maxR2 = 0f;
        foreach (var seq in _atlas.Sequences)
        {
            foreach (var frame in seq.Frames)
            {
                var r = _atlas.Atlas[frame];
                var hw = r.Width / 2f;
                var hh = r.Height / 2f;
                var r2 = hw * hw + hh * hh;
                if (r2 > maxR2) maxR2 = r2;
            }
        }
        return new BoundingCircle(Vector2.Zero, MathF.Sqrt(maxR2));
    }

    public override void Draw(Renderer2D renderer, in Pose2D pose, Color tint, TimeSpan elapsed)
    {
        var source = RegionAt(elapsed);
        var image = _atlas.Atlas.Image;
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
                renderer.DrawImageRotated(image, source, dest, pose.Rotation, rc, pose.Flipped, tint);
            else
                renderer.DrawImageRotated(image, source, dest, pose.Rotation, rc, pose.Flipped);
        }
        else
        {
            if (tinted)
                renderer.DrawImage(image, source, dest, tint);
            else
                renderer.DrawImage(image, source, dest);
        }
    }

    private TimeSpan LocalTime(TimeSpan elapsed)
    {
        if (_resetPending)
        {
            _sequenceStartElapsed = elapsed;
            _resetPending = false;
        }
        return elapsed - _sequenceStartElapsed + Offset;
    }
}
