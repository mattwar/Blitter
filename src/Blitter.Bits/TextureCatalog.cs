namespace Blitter.Bits;

/// <summary>
/// An ordered collection of <see cref="Texture2D"/>s with optional names.
/// A catalog is a non-owning view: its entries (often slices of a shared
/// source image) stay owned by whoever created them, so the catalog never
/// disposes anything.
/// </summary>
public sealed class TextureCatalog
{
    private readonly Texture2D[] _textures;
    private readonly Dictionary<string, int>? _names;
    private readonly string[] _nameList;
    private readonly int _columns;
    private readonly int _rows;

    /// <summary>
    /// Number of entries in the atlas.
    /// </summary>
    public int Count => _textures.Length;

    /// <summary>
    /// Number of columns in the catalog's 2D grid shape. A catalog built
    /// from a flat span or list of regions is laid out as a single row, so
    /// this equals <see cref="Count"/>; <see cref="Grid(Texture2D, int, int)"/>
    /// sets a real multi-row shape. Zero only for an empty catalog.
    /// </summary>
    public int Columns => _columns;

    /// <summary>
    /// Number of rows in the catalog's 2D grid shape. <c>1</c> for a flat
    /// span/region catalog; <see cref="Grid(Texture2D, int, int)"/> sets
    /// the real row count. See <see cref="Columns"/>.
    /// </summary>
    public int Rows => _rows;

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
    /// Gets the <see cref="Texture2D"/> at the given grid position. The
    /// entries are stored linearly in row-major order, so this maps to
    /// index <c>row * Columns + column</c>. Catalogs built from a flat
    /// span or region list are a single row; use
    /// <see cref="Grid(Texture2D, int, int)"/> for a multi-row shape.
    /// </summary>
    public Texture2D this[int column, int row]
    {
        get
        {
            if (_columns == 0)
                throw new InvalidOperationException("TextureCatalog is empty.");
            if ((uint)column >= (uint)_columns)
                throw new ArgumentOutOfRangeException(nameof(column));
            if ((uint)row >= (uint)_rows)
                throw new ArgumentOutOfRangeException(nameof(row));
            return _textures[row * _columns + column];
        }
    }

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
    /// Constructs an <see cref="TextureCatalog"/> from a set of textures and an optional list of names.
    /// The <paramref name="names"/> list assigns names positionally
    /// (<c>names[i]</c> names the texture at index <c>i</c>) and must be no
    /// longer than <paramref name="textures"/>; names left off the end are
    /// simply unnamed. Names must be unique.
    /// Caller retains ownership of <paramref name="textures"/>.
    /// The entries are laid out as a single row (<see cref="Rows"/> = 1);
    /// use <see cref="Grid(Texture2D, int, int)"/> when you need a
    /// multi-row shape.
    /// </summary>
    public TextureCatalog(
        ReadOnlySpan<Texture2D> textures,
        IReadOnlyList<string>? names = null)
        : this(textures, names, textures.Length, textures.Length == 0 ? 0 : 1)
    {
    }

    /// <summary>
    /// Builds a catalog from <c>(texture, name)</c> pairs, so every entry is
    /// named and there is no separate list whose length has to line up with
    /// the textures. Names must be unique. Caller retains ownership of the
    /// textures. The entries are laid out as a single row (<see cref="Rows"/>
    /// = 1); use <see cref="Grid(Texture2D, int, int)"/> for a multi-row shape.
    /// </summary>
    public static TextureCatalog Named(ReadOnlySpan<(Texture2D Texture, string Name)> entries)
    {
        var textures = new Texture2D[entries.Length];
        var names = new string[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            textures[i] = entries[i].Texture;
            names[i] = entries[i].Name;
        }
        return new TextureCatalog(textures, names);
    }

    private TextureCatalog(
        ReadOnlySpan<Texture2D> textures,
        IReadOnlyList<string>? names,
        int columns,
        int rows)
    {
        _textures = new Texture2D[textures.Length];
        for (int i = 0; i < textures.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(textures[i]);
            _textures[i] = textures[i];
        }

        if (columns < 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows < 0) throw new ArgumentOutOfRangeException(nameof(rows));
        if ((columns == 0) != (rows == 0))
            throw new ArgumentException("Provide both columns and rows, or neither.");
        if (columns != 0 && columns * rows != _textures.Length)
            throw new ArgumentException(
                $"Grid shape {columns}x{rows} does not match the {_textures.Length} catalog entries.");
        _columns = columns;
        _rows = rows;

        if (names is not null)
        {
            if (names.Count > _textures.Length)
                throw new ArgumentException(
                    $"Got {names.Count} names for {_textures.Length} textures; there cannot be more names than textures.",
                    nameof(names));
            var map = new Dictionary<string, int>(names.Count, StringComparer.Ordinal);
            var list = new string[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                var name = names[i];
                ArgumentNullException.ThrowIfNull(name);
                if (!map.TryAdd(name, i))
                    throw new ArgumentException(
                        $"Duplicate name '{name}' at index {i}.", nameof(names));
                list[i] = name;
            }
            _names = map;
            _nameList = list;
        }
        else
        {
            _nameList = Array.Empty<string>();
        }
    }

    /// <summary>
    /// Builds an atlas from a single image and a list of sub-rects.
    /// Each rect becomes a slice of <paramref name="image"/>. The slices
    /// are laid out as a single row; use
    /// <see cref="Grid(Texture2D, int, int)"/> for a multi-row shape.
    /// The caller keeps ownership of <paramref name="image"/>.
    /// </summary>
    public static TextureCatalog FromRegions(
        Texture2D image,
        ReadOnlySpan<Rect> regions,
        IReadOnlyList<string>? names = null)
        => FromRegions(image, regions, names,
            regions.Length, regions.Length == 0 ? 0 : 1);

    private static TextureCatalog FromRegions(
        Texture2D image,
        ReadOnlySpan<Rect> regions,
        IReadOnlyList<string>? names,
        int columns,
        int rows)
    {
        ArgumentNullException.ThrowIfNull(image);
        var segs = new Texture2D[regions.Length];
        for (int i = 0; i < regions.Length; i++)
            segs[i] = image.Slice(regions[i]);
        return new TextureCatalog(segs, names, columns, rows);
    }

    /// <summary>
    /// Creates an atlas by splitting an image into a uniform grid of
    /// <paramref name="columns"/> × <paramref name="rows"/> regions. The
    /// cell size is the image size divided by the counts. To slice by a
    /// fixed tile size instead, use <see cref="Tiles"/>.
    /// </summary>
    public static TextureCatalog Grid(Texture2D image, int columns, int rows)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
        var (w, h) = image.Size;
        return GridCore(image, columns, rows, w / columns, h / rows);
    }

    /// <summary>
    /// Creates an atlas by splitting an image into uniform tiles of
    /// <paramref name="tileWidth"/> × <paramref name="tileHeight"/> pixels.
    /// The column and row counts are the image size divided by the tile
    /// size, so any trailing pixels that do not fill a whole tile are
    /// ignored. To slice into a fixed number of columns/rows instead, use
    /// <see cref="Grid(Texture2D, int, int)"/>.
    /// </summary>
    public static TextureCatalog Tiles(Texture2D image, int tileWidth, int tileHeight)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (tileWidth <= 0) throw new ArgumentOutOfRangeException(nameof(tileWidth));
        if (tileHeight <= 0) throw new ArgumentOutOfRangeException(nameof(tileHeight));
        var (w, h) = image.Size;
        return GridCore(image, w / tileWidth, h / tileHeight, tileWidth, tileHeight);
    }

    private static TextureCatalog GridCore(
        Texture2D image,
        int columns,
        int rows,
        int cellWidth,
        int cellHeight)
    {
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));

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
        return FromRegions(image, rects, names: null, columns, rows);
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
        int minColumnGutter = 1)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfLessThan(minRegionWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minRegionHeight, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minRowGutter, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minColumnGutter, 1);
        var (w, h) = image.Size;
        if (w <= 0 || h <= 0)
            return FromRegions(image, ReadOnlySpan<Rect>.Empty, names: null);

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
        return FromRegions(image, rects.ToArray(), names: null);
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
}
