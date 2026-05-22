namespace Blitter.Bits;

/// <summary>
/// A collection of regions over a single <see cref="Image"/>.
/// </summary>
public sealed class Atlas : IDisposable
{
    private readonly Rect[] _regions;
    private readonly Dictionary<string, int>? _names;
    private readonly bool _ownsImage;
    private bool _disposed;

    /// <summary>The backing image. All region rectangles index into this image's pixel space.</summary>
    public Texture2D Image { get; }

    /// <summary>Number of regions in the atlas.</summary>
    public int Count => _regions.Length;

    /// <summary>Looks up a region by zero-based index.</summary>
    public Rect this[int index] => _regions[index];

    /// <summary>
    /// Looks up a region by name.
    /// </summary>
    public Rect this[string name]
    {
        get
        {
            if (_names is null)
                throw new InvalidOperationException("Atlas has no name map.");
            return _regions[_names[name]];
        }
    }

    /// <summary>
    /// Constructs an <see cref="Atlas"/> from an image and a set of regions.
    /// </summary>
    public Atlas(Texture2D image, ReadOnlySpan<Rect> regions, bool ownsImage = true)
        : this(image, regions, names: null, ownsImage)
    {
    }

    /// <summary>
    /// Constructs an <see cref="Atlas"/> from an image, a set of regions, and an optional name-to-index map.   
    /// </summary>
    public Atlas(
        Texture2D image,
        ReadOnlySpan<Rect> regions,
        IReadOnlyDictionary<string, int>? names,
        bool ownsImage = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        _regions = regions.ToArray();
        Image = image;
        _ownsImage = ownsImage;

        if (names is not null)
        {
            _names = new Dictionary<string, int>(names.Count, StringComparer.Ordinal);
            foreach (var kv in names)
            {
                if ((uint)kv.Value >= (uint)_regions.Length)
                    throw new ArgumentOutOfRangeException(nameof(names),
                        $"Name '{kv.Key}' maps to index {kv.Value} which is outside [0, {_regions.Length}).");
                _names.Add(kv.Key, kv.Value);
            }
        }
    }

    /// <summary>
    /// Creates an <see cref="Atlas"/> by splitting an <see cref="Image"/> into a uniform grid of regions.
    /// </summary>
    public static Atlas Grid(Texture2D image, int columns, int rows, bool ownsImage = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
        var (w, h) = image.Size;
        return Grid(image, columns, rows, w / columns, h / rows, ownsImage);
    }

    /// <summary>
    /// Creates an <see cref="Atlas"/> by splitting an <see cref="Image"/> into a uniform grid of regions.
    /// </summary>
    public static Atlas Grid(
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
        return new Atlas(image, rects, ownsImage);
    }

    /// <summary>
    /// Builds an <see cref="Atlas"/> by detecting the grid implicit in
    /// a sprite sheet: rows and columns of fully-transparent pixels
    /// are treated as gutters, and the opaque bands between them
    /// become the cells. Regions are emitted in row-major order
    /// (top-to-bottom, left-to-right).
    /// </summary>
    /// <param name="image">Sheet to scan; must have at least one
    /// transparent gutter between adjacent cells.</param>
    /// <param name="alphaThreshold">Pixels with alpha &lt;= this value
    /// are treated as gutter; 0 keeps fully-transparent only.</param>
    /// <param name="includeEmptyCells">When false (the default), grid
    /// intersections with no opaque pixels are skipped. Set true to
    /// keep them — useful when the atlas intentionally encodes blank
    /// frames at specific positions.</param>
    /// <param name="ownsImage">Whether the returned atlas disposes
    /// <paramref name="image"/>.</param>
    public static Atlas Sense(
        Bitmap image,
        byte alphaThreshold = 0,
        bool includeEmptyCells = false,
        bool ownsImage = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        if (w <= 0 || h <= 0)
            return new Atlas(image, ReadOnlySpan<Rect>.Empty, ownsImage);

        // Per-column / per-row "has any opaque pixel" flags. One pass
        // over the bitmap fills both.
        Span<bool> colHasContent = w <= 1024 ? stackalloc bool[w] : new bool[w];
        Span<bool> rowHasContent = h <= 1024 ? stackalloc bool[h] : new bool[h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (image.GetPixel(x, y).A > alphaThreshold)
                {
                    colHasContent[x] = true;
                    rowHasContent[y] = true;
                }
            }
        }

        // Collapse the flag arrays into runs of opaque indices.
        Span<int> colStarts = stackalloc int[16];
        Span<int> colEnds = stackalloc int[16];
        int colCount = FindRuns(colHasContent, ref colStarts, ref colEnds);

        Span<int> rowStarts = stackalloc int[16];
        Span<int> rowEnds = stackalloc int[16];
        int rowCount = FindRuns(rowHasContent, ref rowStarts, ref rowEnds);

        var rects = new List<Rect>(rowCount * colCount);
        for (int r = 0; r < rowCount; r++)
        {
            for (int c = 0; c < colCount; c++)
            {
                int x0 = colStarts[c], x1 = colEnds[c];
                int y0 = rowStarts[r], y1 = rowEnds[r];
                if (!includeEmptyCells && !HasOpaque(image, x0, y0, x1, y1, alphaThreshold))
                    continue;
                rects.Add(new Rect(x0, y0, x1 - x0, y1 - y0));
            }
        }
        return new Atlas(image, rects.ToArray().AsSpan(), ownsImage);
    }

    private static bool HasOpaque(Bitmap image, int x0, int y0, int x1, int y1, byte alphaThreshold)
    {
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if (image.GetPixel(x, y).A > alphaThreshold) return true;
        return false;
    }

    // Finds runs of 'true' in <paramref name="flags"/> and writes their
    // [start, end) extents. Grows the output spans if needed.
    private static int FindRuns(ReadOnlySpan<bool> flags, ref Span<int> starts, ref Span<int> ends)
    {
        int count = 0;
        int i = 0;
        while (i < flags.Length)
        {
            if (!flags[i]) { i++; continue; }
            int start = i;
            while (i < flags.Length && flags[i]) i++;
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

    /// <summary>
    /// True if a region with the given name is registered.
    /// </summary>
    public bool Contains(string name) => _names is not null && _names.ContainsKey(name);

    /// <summary>
    /// Resolves a name to its region index. Returns <c>false</c> if the
    /// atlas has no name map or the name is not registered.
    /// </summary>
    public bool TryGetIndex(string name, out int index)
    {
        if (_names is not null && _names.TryGetValue(name, out index))
            return true;
        index = -1;
        return false;
    }

    /// <summary>Draws region <paramref name="index"/> into <paramref name="destination"/>.</summary>
    public bool Draw(Renderer2D renderer, int index, Rect destination)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        return renderer.DrawImage(Image, _regions[index], destination);
    }

    /// <summary>Draws the named region into <paramref name="destination"/>.</summary>
    public bool Draw(Renderer2D renderer, string name, Rect destination)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        return renderer.DrawImage(Image, this[name], destination);
    }

    /// <summary>
    /// Disposes the backing <see cref="Image"/> if this atlas owns it.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsImage)
            Image.Dispose();
    }
}
