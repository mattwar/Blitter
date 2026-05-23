using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// A <see cref="Visual2D"/> that plays a named <see cref="AnimationSequence"/>
/// from an <see cref="AnimationCatalog"/>. Set <see cref="Visual2D.State"/>
/// to switch sequences; the new sequence starts from its first frame.
/// </summary>
public sealed class AnimatedVisual2D : Visual2D
{
    private readonly AnimationCatalog _catalog;
    private readonly HitShapeCache _hitShapeCache;
    private BoundingCircle? _boundary;

    // Local playback clock: the elapsed value that the current sequence treats as
    // "time zero". On a State change we mark the clock as pending-reset; the next
    // Draw stamps the base so the new sequence starts from frame 0.
    private string _state;
    private AnimationSequence _current;
    private TimeSpan _sequenceStartElapsed;
    private bool _resetPending;

    public AnimatedVisual2D(AnimationCatalog catalog, TimeSpan phaseOffset = default, HitShapeCache? hitShapeCache = null)
        : this(catalog, catalog?.Names[0]!, phaseOffset, hitShapeCache)
    {
    }

    public AnimatedVisual2D(AnimationCatalog catalog, string initialState, TimeSpan phaseOffset = default, HitShapeCache? hitShapeCache = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrEmpty(initialState);
        if (!catalog.TryGet(initialState, out var seq))
            throw new ArgumentException($"Unknown state '{initialState}'.", nameof(initialState));
        _catalog = catalog;
        _state = initialState;
        _current = seq;
        PhaseOffset = phaseOffset;
        _hitShapeCache = hitShapeCache ?? HitShapeCache.Default;
    }

    private AnimatedVisual2D(AnimationCatalog catalog, string state, AnimationSequence current, TimeSpan phaseOffset, HitShapeCache hitShapeCache)
    {
        _catalog = catalog;
        _state = state;
        _current = current;
        PhaseOffset = phaseOffset;
        _hitShapeCache = hitShapeCache;
    }

    /// <summary>Catalog of sequences this visual draws from.</summary>
    public AnimationCatalog Catalog => _catalog;

    /// <inheritdoc/>
    public override string State
    {
        get => _state;
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            if (value == _state)
                return;
            if (!_catalog.TryGet(value, out var seq))
                throw new ArgumentException($"Unknown state '{value}'.", nameof(value));
            _state = value;
            _current = seq;
            _resetPending = true;
        }
    }

    /// <inheritdoc/>
    public override IReadOnlyList<string> States =>
        _catalog.Names;

    /// <summary>
    /// The <see cref="AnimationSequence"/> currently selected by <see cref="State"/>.
    /// </summary>
    public AnimationSequence CurrentSequence =>
        _current;

    /// <summary>
    /// Phase offset added to local sequence time before picking a frame.
    /// Useful for starting a second host's animation out of phase.
    /// </summary>
    public TimeSpan PhaseOffset { get; }

    /// <summary>
    /// Returns a copy of this visual with a different
    /// <paramref name="phaseOffset"/>.
    /// </summary>
    public AnimatedVisual2D WithPhaseOffset(TimeSpan phaseOffset) =>
        new(_catalog, _state, _current, phaseOffset, _hitShapeCache);

    /// <summary>
    /// Index into the current sequence's frame list at the given host <paramref name="elapsed"/> time.
    /// </summary>
    public int FrameIndexAt(TimeSpan elapsed) =>
        _current.FrameIndexAt(LocalTime(elapsed));

    /// <summary>
    /// Frame drawn for the current sequence at the given time.
    /// </summary>
    public AnimationFrame FrameAt(TimeSpan elapsed) =>
        _current.FrameAt(LocalTime(elapsed));

    /// <summary>
    /// True if the current sequence is <see cref="AnimationLoop.Once"/> and has reached its last frame.
    /// </summary>
    public bool IsAtEnd(TimeSpan elapsed) =>
        _current.IsAtEnd(LocalTime(elapsed));

    /// <inheritdoc/>
    public override BoundingCircle Boundary => 
        _boundary ??= ComputeBoundary();

    /// <inheritdoc/>
    public override HitShape2D HitShape
    {
        get
        {
            var f = _current.Frames[0];
            return _hitShapeCache.GetOrCreateHitShape(f.Texture).Flipped(f.Flip);
        }
    }

    /// <inheritdoc/>
    public override HitShape2D GetHitShapeAt(TimeSpan elapsed)
    {
        var f = _current.FrameAt(LocalTime(elapsed));
        return _hitShapeCache.GetOrCreateHitShape(f.Texture).Flipped(f.Flip);
    }

    /// <summary>
    /// Computes the overall bounding circle for this visual by examining the sizes of all frames
    /// </summary>
    private BoundingCircle ComputeBoundary()
    {
        float maxR2 = 0f;
        foreach (var seq in _catalog.Sequences)
        {
            foreach (var frame in seq.Frames)
            {
                var s = frame.Texture.Size;
                var hw = s.Width / 2f;
                var hh = s.Height / 2f;
                var r2 = hw * hw + hh * hh;
                if (r2 > maxR2) maxR2 = r2;
            }
        }
        return new BoundingCircle(Vector2.Zero, MathF.Sqrt(maxR2));
    }

    /// <inheritdoc/>
    public override void Draw(Renderer2D renderer, in Pose2D pose, Color tint, TimeSpan elapsed, FlipMode flip = FlipMode.None)
    {
        var frame = _current.FrameAt(LocalTime(elapsed));
        var texture = frame.Texture;
        var size = texture.Size;
        var scaledW = size.Width * pose.Scale;
        var scaledH = size.Height * pose.Scale;
        var dest = new Rect(
            pose.Position.X - scaledW / 2f,
            pose.Position.Y - scaledH / 2f,
            scaledW,
            scaledH);
        var source = new Rect(0f, 0f, size.Width, size.Height);
        bool tinted = tint != Color.White;
        // Authoring flip (from the frame) composed with runtime flip
        // (from the caller). FlipMode is a Klein-4 group under XOR;
        // applying H twice cancels back to identity.
        var effectiveFlip = frame.Flip ^ flip;

        if (pose.Rotation != 0f || effectiveFlip != FlipMode.None)
        {
            var rc = new Vector2(scaledW / 2f, scaledH / 2f);
            if (tinted)
                renderer.DrawImageRotated(texture, source, dest, pose.Rotation, rc, effectiveFlip, tint);
            else
                renderer.DrawImageRotated(texture, source, dest, pose.Rotation, rc, effectiveFlip);
        }
        else
        {
            if (tinted)
                renderer.DrawImage(texture, source, dest, tint);
            else
                renderer.DrawImage(texture, source, dest);
        }
    }

    private TimeSpan LocalTime(TimeSpan elapsed)
    {
        if (_resetPending)
        {
            _sequenceStartElapsed = elapsed;
            _resetPending = false;
        }
        return elapsed - _sequenceStartElapsed + PhaseOffset;
    }
}
