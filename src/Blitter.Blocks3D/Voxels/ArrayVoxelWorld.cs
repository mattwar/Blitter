namespace Blitter.Blocks3D;

/// <summary>
/// A fixed-size dense <see cref="IVoxelWorld"/> backed by a flat <see cref="int"/> array. 
/// Suited to tests and small/bounded worlds.
/// </summary>
public sealed class ArrayVoxelWorld : IVoxelWorld
{
    private readonly int[] _cells;

    public ArrayVoxelWorld(int width, int height, int depth, VoxelPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (depth <= 0)
            throw new ArgumentOutOfRangeException(nameof(depth));

        Width = width;
        Height = height;
        Depth = depth;
        Palette = palette;
        _cells = new int[width * height * depth];
    }

    /// <summary>Number of cells along X.</summary>
    public int Width { get; }

    /// <summary>Number of cells along Y.</summary>
    public int Height { get; }

    /// <summary>Number of cells along Z.</summary>
    public int Depth { get; }

    /// <inheritdoc/>
    public VoxelPalette Palette { get; }

    /// <inheritdoc/>
    public event EventHandler<VoxelChangeEventArgs>? VoxelsChanged;

    /// <inheritdoc/>
    public int GetVoxel(int x, int y, int z)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || (uint)z >= (uint)Depth)
            return 0;
        return _cells[Index(x, y, z)];
    }

    /// <inheritdoc/>
    public bool SetVoxel(int x, int y, int z, int id)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || (uint)z >= (uint)Depth)
            return false;
        var i = Index(x, y, z);
        if (_cells[i] == id)
            return false;
        _cells[i] = id;
        VoxelsChanged?.Invoke(this, VoxelChangeEventArgs.Single(x, y, z));
        return true;
    }

    /// <summary>
    /// Bulk-fills the cell range <c>[minX..maxX] × [minY..maxY] × [minZ..maxZ]</c>
    /// (inclusive) with <paramref name="id"/> and raises a single
    /// <see cref="VoxelsChanged"/> for the entire bounding box. The
    /// range is clipped to the world. Returns the number of cells
    /// actually written.
    /// </summary>
    public int Fill(int minX, int minY, int minZ, int maxX, int maxY, int maxZ, int id)
    {
        var x0 = Math.Max(minX, 0);
        var y0 = Math.Max(minY, 0);
        var z0 = Math.Max(minZ, 0);
        var x1 = Math.Min(maxX, Width - 1);
        var y1 = Math.Min(maxY, Height - 1);
        var z1 = Math.Min(maxZ, Depth - 1);
        if (x0 > x1 || y0 > y1 || z0 > z1)
            return 0;

        var written = 0;
        for (int z = z0; z <= z1; z++)
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            var i = Index(x, y, z);
            if (_cells[i] != id)
            {
                _cells[i] = id;
                written++;
            }
        }

        if (written > 0)
            VoxelsChanged?.Invoke(this, new VoxelChangeEventArgs(x0, y0, z0, x1, y1, z1));
        return written;
    }

    // Row-major: x varies fastest (best cache locality for x-axis scans).
    private int Index(int x, int y, int z) => (z * Height + y) * Width + x;
}
