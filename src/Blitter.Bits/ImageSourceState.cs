using System.Collections.Immutable;

namespace Blitter.Bits;

/// <summary>
/// Declarative description of one named look of an <see cref="ImageSource"/>:
/// either a single tile (the <see cref="Tile"/> shorthand) or an ordered list
/// of animation <see cref="Frames"/>, played at <see cref="FrameDuration"/>
/// with the chosen <see cref="Loop"/> behaviour. <see cref="FilePath"/> and
/// <see cref="TileSize"/> are inherited from the root <see cref="ImageSource"/>
/// when unset, so a shared sheet is declared once and each state need only pick
/// its cell(s). A bare path string or <c>(Column, Row)</c> tuple converts
/// implicitly to a single-frame state.
/// </summary>
public sealed class ImageSourceState
{
    /// <summary>
    /// Path to the image file. Inherited from the root source when unset, and
    /// passed down to this state's <see cref="Frames"/>.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// The size, in pixels, of each grid cell. Inherited from the root source
    /// when unset, and passed down to this state's <see cref="Frames"/>.
    /// </summary>
    public (int Width, int Height)? TileSize { get; set; }

    /// <summary>
    /// Single-frame shorthand: the <c>(Column, Row)</c> of the one tile this
    /// state shows. Mutually exclusive with <see cref="Index"/> and
    /// <see cref="Frames"/>. Leave all unset (with empty <see cref="Frames"/>) to
    /// use the whole image.
    /// </summary>
    public (int Column, int Row)? Tile { get; set; }

    /// <summary>
    /// Single-frame shorthand: the zero-based index of the one frame this state
    /// shows, the 1D alternative to <see cref="Tile"/> (a grid cell when a
    /// <see cref="TileSize"/> is in effect, otherwise an auto-sensed region).
    /// Mutually exclusive with <see cref="Tile"/> and <see cref="Frames"/>.
    /// </summary>
    public int? Index { get; set; }

    /// <summary>
    /// An already-built texture to show as a single static frame, the escape
    /// hatch for when you have the exact image in hand. Mutually exclusive with
    /// <see cref="Tile"/>, <see cref="Index"/>, and <see cref="Frames"/>; a bare
    /// <see cref="Texture2D"/> or <see cref="AnimationFrame"/> converts implicitly.
    /// </summary>
    public Texture2D? Texture { get; set; }

    /// <summary>
    /// An already-built animation sequence to use directly, the escape hatch for
    /// when you have the exact sequence in hand (e.g. one you built from a
    /// <see cref="TextureCatalog"/> you sensed yourself). Taken as-is; mutually
    /// exclusive with every other selector. A bare <see cref="AnimationSequence"/>
    /// converts implicitly.
    /// </summary>
    public AnimationSequence? Sequence { get; set; }

    /// <summary>
    /// Mirror applied to every frame of this state, composed (XOR) with each
    /// frame's own flip. Handy for a left-facing variant of a right-facing
    /// sheet.
    /// </summary>
    public FlipMode Flip { get; set; }

    /// <summary>How long each frame is held. Irrelevant for a single frame.</summary>
    public TimeSpan FrameDuration
    {
        get => _frameDuration ?? DefaultFrameDuration;
        set => _frameDuration = value;
    }

    private TimeSpan? _frameDuration;
    private static readonly TimeSpan DefaultFrameDuration = TimeSpan.FromSeconds(0.1);

    /// <summary>
    /// Total play time for the whole state, divided evenly across its
    /// <see cref="Frames"/> to set the per-frame cadence — so you can declare
    /// "this five-frame jump lasts the airtime" without hand-computing each
    /// frame's slice. Mutually exclusive with <see cref="FrameDuration"/>; leave
    /// unset to use <see cref="FrameDuration"/> instead. Ignored for a single
    /// frame.
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>Behaviour when the sequence reaches its end.</summary>
    public AnimationLoop Loop { get; set; } = AnimationLoop.Loop;

    /// <summary>
    /// Ordered animation frames. When non-empty, drives the look (and
    /// <see cref="Tile"/>/<see cref="Index"/> must be unset); otherwise the
    /// single <see cref="Tile"/> or <see cref="Index"/> shorthand is used.
    /// </summary>
    public List<ImageSourceFrame> Frames { get; } = new();

    /// <summary>Treats a bare path string as a single whole-image state.</summary>
    public static implicit operator ImageSourceState(string path) =>
        new() { FilePath = path };

    /// <summary>Treats a bare <c>(Column, Row)</c> tuple as a single-tile state.</summary>
    public static implicit operator ImageSourceState((int Column, int Row) tile) =>
        new() { Tile = tile };

    /// <summary>Treats a bare integer as a single <see cref="Index"/> state.</summary>
    public static implicit operator ImageSourceState(int index) =>
        new() { Index = index };

    /// <summary>Treats an already-built texture as a single static-frame state.</summary>
    public static implicit operator ImageSourceState(Texture2D texture) =>
        new() { Texture = texture };

    /// <summary>Treats an already-built animation frame as a single static-frame state.</summary>
    public static implicit operator ImageSourceState(AnimationFrame frame) =>
        new() { Texture = frame.Texture, Flip = frame.Flip };

    /// <summary>Treats an already-built sequence as the state's animation.</summary>
    public static implicit operator ImageSourceState(AnimationSequence sequence) =>
        new() { Sequence = sequence };

    // Resolves into an AnimationSequence against the inherited context,
    // overriding with this state's own file path / tile size / flip.
    internal AnimationSequence Resolve(in ImageSourceContext context)
    {
        if (Sequence is not null)
        {
            if (Frames.Count > 0 || Tile is not null || Index is not null
                || Texture is not null || FilePath is not null)
                throw new InvalidOperationException(
                    $"An {nameof(ImageSourceState)} sets either an explicit {nameof(Sequence)} or other frame sources, not both.");
            return Sequence;
        }

        var ctx = context.Inherit(FilePath, TileSize, Flip);

        if (Duration is not null && _frameDuration is not null)
            throw new InvalidOperationException(
                $"An {nameof(ImageSourceState)} sets either {nameof(Duration)} (total) or {nameof(FrameDuration)} (per frame), not both.");

        ImmutableArray<AnimationFrame> frames;
        if (Frames.Count > 0)
        {
            if (Tile is not null || Index is not null || Texture is not null)
                throw new InvalidOperationException(
                    $"An {nameof(ImageSourceState)} sets either a single frame ({nameof(Tile)}/{nameof(Index)}/{nameof(Texture)}) or {nameof(Frames)}, not both.");

            var builder = ImmutableArray.CreateBuilder<AnimationFrame>(Frames.Count);
            foreach (var frame in Frames)
            {
                ArgumentNullException.ThrowIfNull(frame);
                builder.Add(frame.Resolve(ctx));
            }
            frames = builder.MoveToImmutable();
        }
        else if (Texture is not null)
        {
            if (Tile is not null || Index is not null)
                throw new InvalidOperationException(
                    $"An {nameof(ImageSourceState)} sets either an explicit {nameof(Texture)} or a {nameof(Tile)}/{nameof(Index)}, not both.");
            frames = ImmutableArray.Create(new AnimationFrame(Texture, ctx.Flip));
        }
        else
        {
            var texture = ImageSource.ResolveTexture(ctx.FilePath, Tile, Index, ctx.TileSize);
            frames = ImmutableArray.Create(new AnimationFrame(texture, ctx.Flip));
        }

        // Total Duration is split evenly across the frames; otherwise each
        // frame is held for the explicit (or default) FrameDuration.
        var frameDuration = Duration is { } total
            ? TimeSpan.FromTicks(total.Ticks / frames.Length)
            : FrameDuration;

        return new AnimationSequence(frames, frameDuration, Loop);
    }
}
