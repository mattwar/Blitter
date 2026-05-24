namespace Blitter.Bits;

/// <summary>
/// An ordered collection of <see cref="Texture2D"/>s with optional names.
/// </summary>
public sealed class TextureCatalog : IDisposable
{
    private readonly Texture2D[] _textures;
    private readonly Dictionary<string, int>? _names;
    private readonly string[] _nameList;
    private readonly IDisposable[]? _owned;
    private bool _disposed;

    /// <summary>
    /// Number of entries in the atlas.
    /// </summary>
    public int Count => _textures.Length;

    /// <summary>
    /// Names registered in this atlas, in the order they were supplied.
    /// Empty when the atlas has no name map.
    /// </summary>
    public IReadOnlyList<string> Names => _nameList;

    /// <summary>
    /// Gets the <see cref="Texture2D"/> at the specified zero-based index.
    /// </summary>
    public Texture2D this[int index] => _textures[index];

    /// <summary>
    /// Gets the <see cref="Texture2D"/> registered with the specified name,
    /// or throws an exception if the name is not found in the atlas.
    /// </summary>
    public Texture2D this[string name]
    {
        get
        {
            if (_names is null)
                throw new InvalidOperationException("TextureCatalog has no name map.");
            return _textures[_names[name]];
        }
    }

    /// <summary>
    /// Constructs an <see cref="TextureCatalog"/> from a set of textures and an optional name-to-index map.
    /// Caller retains ownership of <paramref name="textures"/>; the atlas disposes nothing.
    /// </summary>
    public TextureCatalog(
        ReadOnlySpan<Texture2D> textures,
        IReadOnlyDictionary<string, int>? names = null)
        : this(textures, names, owned: null)
    {
    }

    private TextureCatalog(
        ReadOnlySpan<Texture2D> textures,
        IReadOnlyDictionary<string, int>? names,
        IDisposable[]? owned)
    {
        _textures = new Texture2D[textures.Length];
        for (int i = 0; i < textures.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(textures[i]);
            _textures[i] = textures[i];
        }
        _owned = owned;

        if (names is not null)
        {
            _names = new Dictionary<string, int>(names.Count, StringComparer.Ordinal);
            var list = new string[names.Count];
            int n = 0;
            foreach (var kv in names)
            {
                if ((uint)kv.Value >= (uint)_textures.Length)
                    throw new ArgumentOutOfRangeException(nameof(names),
                        $"Name '{kv.Key}' maps to index {kv.Value} which is outside [0, {_textures.Length}).");
                _names.Add(kv.Key, kv.Value);
                list[n++] = kv.Key;
            }
            _nameList = list;
        }
        else
        {
            _nameList = Array.Empty<string>();
        }
    }

    /// <summary>
    /// Builds an atlas from a single image and a list of sub-rects.
    /// Each rect becomes a slice of <paramref name="image"/>.
    /// </summary>
    public static TextureCatalog FromRegions(
        Texture2D image,
        ReadOnlySpan<Rect> regions,
        IReadOnlyDictionary<string, int>? names = null,
        bool ownsImage = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        var segs = new Texture2D[regions.Length];
        for (int i = 0; i < regions.Length; i++)
            segs[i] = image.Slice(regions[i]);
        return new TextureCatalog(segs, names, ownsImage ? [image] : null);
    }

    /// <summary>
    /// Creates an atlas by splitting an image into a uniform grid of regions.
    /// </summary>
    public static TextureCatalog Grid(Texture2D image, int columns, int rows, bool ownsImage = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
        var (w, h) = image.Size;
        return Grid(image, columns, rows, w / columns, h / rows, ownsImage);
    }

    /// <summary>
    /// Creates an atlas by splitting an image into a uniform grid of regions
    /// with explicit cell dimensions.
    /// </summary>
    public static TextureCatalog Grid(
        Texture2D image,
        int columns,
        int rows,
        int cellWidth,
        int cellHeight,
        bool ownsImage = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
        if (cellWidth <= 0) throw new ArgumentOutOfRangeException(nameof(cellWidth));
        if (cellHeight <= 0) throw new ArgumentOutOfRangeException(nameof(cellHeight));

        var rects = new Rect[columns * rows];
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                rects[row * columns + col] = new Rect(
                    col * cellWidth,
                    row * cellHeight,
                    cellWidth,
                    cellHeight);
            }
        }
        return FromRegions(image, rects, names: null, ownsImage);
    }

    /// <summary>
    /// Builds an atlas by detecting layout implicit in a sprite sheet.
    /// First, rows of fully-transparent pixels split the image into
    /// horizontal bands. Within each band, columns of transparent
    /// pixels split the band into individual regions. This handles
    /// sheets whose rows are not perfectly column-aligned.
    /// <para/>
    /// <paramref name="minRowGutter"/> and <paramref name="minColumnGutter"/>
    /// set the minimum number of consecutive transparent rows or
    /// columns that count as a real separator — shorter breaks are
    /// absorbed into the surrounding region. Useful when JPEG halos
    /// or shadow fragments leave a few-pixel gap inside a single
    /// sprite's extent.
    /// <para/>
    /// <paramref name="minRegionWidth"/> and
    /// <paramref name="minRegionHeight"/> drop regions smaller than
    /// the given size in either dimension — useful for ignoring
    /// stray noise pixels.
    /// Regions are numbered band-by-band, left-to-right within each
    /// band.
    /// </summary>
    public static TextureCatalog Sense(
        ReadableTexture2D image,
        byte alphaThreshold = 0,
        int minRegionWidth = 1,
        int minRegionHeight = 1,
        int minRowGutter = 1,
        int minColumnGutter = 1,
        bool ownsImage = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfLessThan(minRegionWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minRegionHeight, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minRowGutter, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minColumnGutter, 1);
        var (w, h) = image.Size;
        if (w <= 0 || h <= 0)
            return FromRegions(image, ReadOnlySpan<Rect>.Empty, names: null, ownsImage);

        // First pass: per-row "has any opaque pixel" flag, used to
        // partition the image into horizontal bands.
        Span<bool> rowHasContent = h <= 1024 ? stackalloc bool[h] : new bool[h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (image.GetPixel(x, y).A > alphaThreshold)
                {
                    rowHasContent[y] = true;
                    break;
                }
            }
        }

        Span<int> bandStarts = stackalloc int[16];
        Span<int> bandEnds = stackalloc int[16];
        int bandCount = FindRuns(rowHasContent, ref bandStarts, ref bandEnds, minRowGutter);

        // Second pass: for each band, project only that band's rows
        // onto the x-axis and find column runs. Each run becomes a
        // region whose y-extent spans the whole band.
        var rects = new List<Rect>();
        Span<bool> colHasContent = w <= 1024 ? stackalloc bool[w] : new bool[w];
        Span<int> colStarts = stackalloc int[16];
        Span<int> colEnds = stackalloc int[16];
        for (int b = 0; b < bandCount; b++)
        {
            int y0 = bandStarts[b], y1 = bandEnds[b];
            if (y1 - y0 < minRegionHeight) continue;

            colHasContent.Clear();
            for (int y = y0; y < y1; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!colHasContent[x] && image.GetPixel(x, y).A > alphaThreshold)
                        colHasContent[x] = true;
                }
            }

            int colCount = FindRuns(colHasContent, ref colStarts, ref colEnds, minColumnGutter);
            for (int c = 0; c < colCount; c++)
            {
                int x0 = colStarts[c], x1 = colEnds[c];
                if (x1 - x0 < minRegionWidth) continue;
                rects.Add(new Rect(x0, y0, x1 - x0, y1 - y0));
            }
        }
        return FromRegions(image, rects.ToArray(), names: null, ownsImage);
    }

    // Finds runs of 'true' in <paramref name="flags"/> and writes their
    // [start, end) extents. Runs separated by a gap of fewer than
    // <paramref name="minGap"/> false entries are merged — the merged
    // run's extent then includes the bridged false entries.
    // Grows the output spans if needed.
    private static int FindRuns(ReadOnlySpan<bool> flags, ref Span<int> starts, ref Span<int> ends, int minGap = 1)
    {
        int count = 0;
        int i = 0;
        while (i < flags.Length)
        {
            if (!flags[i]) { i++; continue; }
            int start = i;
            while (i < flags.Length && flags[i]) i++;
            // Bridge to the previous run if the gap is below the threshold.
            if (count > 0 && start - ends[count - 1] < minGap)
            {
                ends[count - 1] = i;
                continue;
            }
            if (count == starts.Length)
            {
                var newStarts = new int[starts.Length * 2];
                var newEnds = new int[ends.Length * 2];
                starts.CopyTo(newStarts);
                ends.CopyTo(newEnds);
                starts = newStarts;
                ends = newEnds;
            }
            starts[count] = start;
            ends[count] = i;
            count++;
        }
        return count;
    }

    /// <summary>True if an entry with the given name is registered.</summary>
    public bool Contains(string name) => _names is not null && _names.ContainsKey(name);

    /// <summary>
    /// Resolves a name to its entry index. Returns <c>false</c> if the atlas
    /// has no name map or the name is not registered.
    /// </summary>
    public bool TryGetIndex(string name, out int index)
    {
        if (_names is not null && _names.TryGetValue(name, out index))
            return true;
        index = -1;
        return false;
    }

    /// <summary>
    /// Disposes any source images this atlas's factories took ownership of.
    /// Textures supplied directly to the general constructor are left alone.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_owned is null) return;
        foreach (var d in _owned)
            d.Dispose();
    }
}
