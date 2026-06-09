using System.Collections.Concurrent;
using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// How an <see cref="ImageSource"/>'s collision <see cref="HitShape2D"/> is
/// derived from its image.
/// </summary>
public enum HitShapeHint
{
    /// <summary>Derive from the image's opaque pixels (the default).</summary>
    Auto,

    /// <summary>An inscribed circle centred on the tile.</summary>
    Circle,

    /// <summary>A box covering the whole tile.</summary>
    Box,

    /// <summary>No collision; the image is purely decorative.</summary>
    None,
}

// Resolution context threaded top-down through the source tree
// (ImageSource -> ImageSourceState -> ImageSourceFrame). Each level overrides
// the inherited file path / tile size where set and composes its flip (XOR),
// so a sheet declared once at the root flows down to every frame without any
// back pointers.
internal readonly record struct ImageSourceContext(
    string? FilePath,
    (int Width, int Height)? TileSize,
    FlipMode Flip)
{
    public ImageSourceContext Inherit(
        string? filePath,
        (int Width, int Height)? tileSize,
        FlipMode flip) =>
        new(filePath ?? FilePath, tileSize ?? TileSize, Flip ^ flip);
}

/// <summary>
/// A declarative description of a sprite's drawable image. In its simplest
/// form it is a single picture — a <see cref="FilePath"/> with an optional
/// <see cref="Tile"/> selected from a grid — and <see cref="Visual"/> yields a
/// <see cref="TextureVisual2D"/>. Adding named <see cref="this[string]">states</see>
/// turns it into a multi-look source whose <see cref="Visual"/> is an
/// <see cref="AnimatedVisual2D"/>: each state is one look (a single tile or an
/// animation of frames) and <see cref="Visual2D.State"/> selects between them.
/// </summary>
/// <remarks>
/// The tree has three declarative levels — <see cref="ImageSource"/> (the whole
/// visual), <see cref="ImageSourceState"/> (one named look), and
/// <see cref="ImageSourceFrame"/> (one picture). <see cref="FilePath"/> and
/// <see cref="TileSize"/> declared at any level are inherited by the levels
/// below, so a shared sheet is declared once at the top and each state/frame
/// need only pick its cell.
/// <para>
/// Source bitmaps are cached by resolved path for the lifetime of the
/// application, so repeated sources for the same file share one bitmap. The
/// cache never disposes anything; <see cref="Evict"/> and <see cref="Clear"/>
/// only drop cached mappings so the next load re-reads the file. Paths are
/// resolved against <see cref="Application.AssetFolder"/>.
/// </para>
/// </remarks>
public sealed class ImageSource
{
    private static readonly ConcurrentDictionary<string, Bitmap> s_cache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, TextureCatalog> s_sensed = new(StringComparer.Ordinal);

    /// <summary>
    /// Path to the image file, resolved against <see cref="Application.AssetFolder"/>.
    /// When this source has states, this is the shared sheet inherited by them.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// An already-built texture to use directly, the escape hatch for when you
    /// have the exact image in hand (e.g. a slice from a <see cref="TextureCatalog"/>
    /// you sensed yourself). Mutually exclusive with <see cref="FilePath"/>,
    /// <see cref="Tile"/>, and <see cref="Index"/>; a bare <see cref="Texture2D"/>
    /// converts implicitly.
    /// </summary>
    public Texture2D? Texture { get; set; }

    /// <summary>
    /// The zero-based <c>(Column, Row)</c> of a fixed-size grid cell to select
    /// for a single image (no states). Setting a tile declares a grid of
    /// fixed-size cells, so <see cref="TileSize"/> must be set too. Mutually
    /// exclusive with <see cref="Index"/>; leave both unset for the whole image.
    /// </summary>
    public (int Column, int Row)? Tile { get; set; }

    /// <summary>
    /// The zero-based index of a single frame to select, the 1D alternative to
    /// <see cref="Tile"/>. With a fixed <see cref="TileSize"/> it picks the Nth
    /// cell of the grid in row-major order (wrapping across rows); without a
    /// <see cref="TileSize"/> the sheet is auto-sensed and it picks the Nth
    /// detected region. Mutually exclusive with <see cref="Tile"/>.
    /// </summary>
    public int? Index { get; set; }

    /// <summary>
    /// The size, in pixels, of each grid cell. Setting it declares a fixed
    /// grid: <see cref="Tile"/> selects a cell by column/row and <see cref="Index"/>
    /// by linear position. Leaving it unset means <see cref="Index"/> instead
    /// selects an auto-sensed region. Inherited by any states/frames below.
    /// </summary>
    public (int Width, int Height)? TileSize { get; set; }

    /// <summary>
    /// How the collision <see cref="HitShape2D"/> is derived from the image.
    /// Applies to the single-image (no states) form; when states are present
    /// collision shapes are derived per frame and this hint is ignored.
    /// </summary>
    public HitShapeHint Hit { get; set; } = HitShapeHint.Auto;

    // Named states, in declaration order. Present only when this source
    // describes several named looks rather than a single image.
    private Dictionary<string, ImageSourceState>? _states;
    private List<string>? _stateOrder;

    /// <summary>
    /// Named looks for this source. Accessing a name that does not yet exist
    /// creates and stores an empty state, so it can be populated with an object
    /// initializer (<c>["idle"] = { Tile = (0, 0) }</c>); a state assigned by
    /// value (a path string or tuple converts implicitly) replaces any existing
    /// one. Each state inherits this source's <see cref="FilePath"/> and
    /// <see cref="TileSize"/>. When any states are present,
    /// <see cref="ToVisual2D"/> produces an <see cref="AnimatedVisual2D"/> whose
    /// <see cref="Visual2D.State"/> selects the named look; the first declared
    /// state is the initial one.
    /// </summary>
    public ImageSourceState this[string state]
    {
        get
        {
            ArgumentException.ThrowIfNullOrEmpty(state);
            _states ??= new(StringComparer.Ordinal);
            _stateOrder ??= new();
            if (!_states.TryGetValue(state, out var existing))
            {
                existing = new ImageSourceState();
                _states[state] = existing;
                _stateOrder.Add(state);
            }
            return existing;
        }
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(state);
            ArgumentNullException.ThrowIfNull(value);
            _states ??= new(StringComparer.Ordinal);
            _stateOrder ??= new();
            if (!_states.ContainsKey(state))
                _stateOrder.Add(state);
            _states[state] = value;
        }
    }

    /// <summary>
    /// Whether this source describes several named looks rather than a single
    /// image. When true, <see cref="ToVisual2D"/> yields an
    /// <see cref="AnimatedVisual2D"/>.
    /// </summary>
    public bool HasStates => _states is { Count: > 0 };

    // The materialised visual, locked in on first read of Visual (or set
    // explicitly). Null until then.
    private Visual2D? _visual;

    /// <summary>
    /// The drawable <see cref="Visual2D"/> this source describes. On first read
    /// it is materialised from the other properties via <see cref="ToVisual2D"/>
    /// and locked in, so subsequent changes to the descriptor are not reflected.
    /// Assigning a visual explicitly overrides materialisation. An empty source
    /// (no <see cref="FilePath"/> and no states) yields <c>null</c>, so an
    /// unconfigured slot simply draws nothing.
    /// </summary>
    public Visual2D? Visual
    {
        get
        {
            if (_visual is not null)
                return _visual;
            if (FilePath is null && Texture is null && !HasStates)
                return null;
            return _visual = ToVisual2D();
        }
        set => _visual = value;
    }

    /// <summary>
    /// Drops the cached bitmap mapping for <paramref name="path"/> (resolved
    /// against <see cref="Application.AssetFolder"/>) so the next source for
    /// that path re-reads the file. Already materialised visuals keep their
    /// existing bitmap; nothing is disposed.
    /// </summary>
    public static void Evict(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var resolved = Application.Current.ResolveAssetPath(path);
        s_cache.TryRemove(resolved, out _);
        s_sensed.TryRemove(resolved, out _);
    }

    /// <summary>
    /// Drops all cached bitmap mappings so subsequent loads re-read their
    /// files. Already materialised visuals are unaffected; nothing is disposed.
    /// </summary>
    public static void Clear()
    {
        s_cache.Clear();
        s_sensed.Clear();
    }

    // Shared (cached) load-and-slice used by every level of the tree. The
    // file path and tile size are already resolved (inherited) by the caller.
    internal static Texture2D ResolveTexture(
        string? filePath,
        (int Column, int Row)? tile,
        int? index,
        (int Width, int Height)? tileSize)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new InvalidOperationException($"{nameof(ImageSource)} requires a {nameof(FilePath)}.");
        if (tile is not null && index is not null)
            throw new InvalidOperationException(
                $"An {nameof(ImageSource)} sets either {nameof(Tile)} or {nameof(Index)}, not both.");

        var resolved = Application.Current.ResolveAssetPath(filePath);
        var bitmap = s_cache.GetOrAdd(resolved, static p => Bitmap.Load(p));

        if (tile is not null)
        {
            if (tileSize is null)
                throw new InvalidOperationException(
                    $"{nameof(Tile)} requires {nameof(TileSize)} to be set.");
            var (col, row) = tile.Value;
            var (tw, th) = tileSize.Value;
            return bitmap.Slice(new Rect(col * tw, row * th, tw, th));
        }

        if (index is not null)
        {
            int i = index.Value;
            ArgumentOutOfRangeException.ThrowIfNegative(i, nameof(Index));

            if (tileSize is not null)
            {
                // Fixed grid: index wraps row-major across the sheet's columns.
                var (tw, th) = tileSize.Value;
                var (bw, _) = bitmap.Size;
                int columns = Math.Max(1, bw / tw);
                int col = i % columns, row = i / columns;
                return bitmap.Slice(new Rect(col * tw, row * th, tw, th));
            }

            // No fixed grid: index picks an auto-sensed region.
            var catalog = ResolveSensedCatalog(resolved, bitmap);
            if (i >= catalog.Count)
                throw new ArgumentOutOfRangeException(nameof(Index),
                    $"{nameof(Index)} {i} is out of range; the sensed sheet has {catalog.Count} region(s).");
            return catalog[i];
        }

        // No selection: the whole image.
        return bitmap;
    }

    // Senses (and caches) the sheet's regions with default settings. The
    // catalog is a non-owning view over the already-cached source bitmap.
    private static TextureCatalog ResolveSensedCatalog(string resolvedPath, Bitmap bitmap) =>
        s_sensed.GetOrAdd(resolvedPath, static (_, bmp) => TextureCatalog.Sense(bmp), bitmap);

    /// <summary>
    /// Loads (and caches) the source bitmap and selects the configured
    /// <see cref="Tile"/> or <see cref="Index"/>, or returns an explicit
    /// <see cref="Texture"/>. Only valid for a single-image source; throws if
    /// this source has <see cref="this[string]">states</see>.
    /// </summary>
    public Texture2D ToTexture()
    {
        if (HasStates)
            throw new InvalidOperationException(
                $"This {nameof(ImageSource)} has states; read {nameof(Visual)} instead.");
        if (Texture is not null)
        {
            if (FilePath is not null || Tile is not null || Index is not null)
                throw new InvalidOperationException(
                    $"An {nameof(ImageSource)} sets either an explicit {nameof(Texture)} or a {nameof(FilePath)}/{nameof(Tile)}/{nameof(Index)}, not both.");
            return Texture;
        }
        return ResolveTexture(FilePath, Tile, Index, TileSize);
    }

    /// <summary>
    /// Materialises this source into a <see cref="Visual2D"/>. A single-image
    /// source (no states) yields a <see cref="TextureVisual2D"/> honouring the
    /// <see cref="Hit"/> hint. A source with states yields an
    /// <see cref="AnimatedVisual2D"/> whose <see cref="Visual2D.State"/> selects
    /// the named look; in that case collision shapes are derived per frame and
    /// the <see cref="Hit"/> hint is ignored.
    /// </summary>
    public Visual2D ToVisual2D()
    {
        if (HasStates)
            return ToAnimatedVisual2D();

        var texture = ToTexture();
        var (w, h) = texture.Size;
        return Hit switch
        {
            HitShapeHint.Circle => new TextureVisual2D(
                texture, new CircleHitShape2D(Vector2.Zero, MathF.Min(w, h) / 2f)),
            HitShapeHint.Box => new TextureVisual2D(
                texture, new BoxHitShape2D(Vector2.Zero, new Vector2(w / 2f, h / 2f))),
            HitShapeHint.None => new TextureVisual2D(texture, HitShape2D.None),
            _ => new TextureVisual2D(texture),
        };
    }

    // Builds an AnimatedVisual2D from the named states, in declaration order.
    // The root's FilePath / TileSize seed the resolution context that each
    // state (and its frames) inherits.
    private AnimatedVisual2D ToAnimatedVisual2D()
    {
        var context = new ImageSourceContext(FilePath, TileSize, FlipMode.None);
        var sequences = new List<KeyValuePair<string, AnimationSequence>>(_stateOrder!.Count);
        foreach (var name in _stateOrder)
            sequences.Add(new KeyValuePair<string, AnimationSequence>(
                name, _states![name].Resolve(context)));

        return new AnimatedVisual2D(new AnimationCatalog(sequences));
    }

    /// <summary>Treats a bare path string as a whole-image source.</summary>
    public static implicit operator ImageSource(string path) =>
        new() { FilePath = path };

    /// <summary>Treats a bare texture as a single-image source.</summary>
    public static implicit operator ImageSource(Texture2D texture) =>
        new() { Texture = texture };

    /// <summary>Materialises the source into a drawable visual.</summary>
    public static implicit operator Visual2D(ImageSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.ToVisual2D();
    }

    /// <summary>Materialises a single-image source into its (sliced) texture.</summary>
    public static implicit operator Texture2D(ImageSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.ToTexture();
    }
}
