namespace Blitter.Blocks3D;

/// <summary>
/// A 3D view over a contiguous region of voxel storage. Wraps a borrowed
/// <see cref="VoxelInfo"/> span covering an inclusive <see cref="Bounds"/>
/// box and addresses voxels by their absolute voxel coordinate, mapping them
/// onto the linear storage for you. Iterate <see cref="Bounds"/> and assign
/// via <c>buffer[x, y, z] = type</c>. Write a <see cref="VoxelType"/> (it
/// converts implicitly) into any voxel. This is a borrowed view; do not
/// retain it past the call it was handed to.
/// </summary>
public readonly ref struct VoxelBuffer
{
    private readonly Span<VoxelInfo> _voxels;
    private readonly int _sizeX;
    private readonly int _sizeY;
    private readonly int _sizeZ;

    /// <summary>
    /// Wraps <paramref name="voxels"/> as the region covering the inclusive
    /// <paramref name="bounds"/>. The span length must equal the number of
    /// voxels in the box.
    /// </summary>
    public VoxelBuffer(Span<VoxelInfo> voxels, in VoxelBox bounds)
    {
        int sizeX = bounds.Max.X - bounds.Min.X + 1;
        int sizeY = bounds.Max.Y - bounds.Min.Y + 1;
        int sizeZ = bounds.Max.Z - bounds.Min.Z + 1;
        if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
            throw new ArgumentException("Bounds must be non-empty.", nameof(bounds));
        int volume = bounds.Volume;
        if (voxels.Length != volume)
            throw new ArgumentException(
                $"Span length {voxels.Length} must equal the bounds volume {volume}.",
                nameof(voxels));
        _voxels = voxels;
        Bounds = bounds;
        _sizeX = sizeX;
        _sizeY = sizeY;
        _sizeZ = sizeZ;
    }

    /// <summary>
    /// The inclusive voxel extent this buffer covers. Iterate this to visit
    /// every voxel in the region.
    /// </summary>
    public VoxelBox Bounds { get; }

    /// <summary>
    /// Gets or sets the voxel at coordinate (x, y, z). The coordinate must
    /// lie within <see cref="Bounds"/>.
    /// </summary>
    public VoxelInfo this[int x, int y, int z]
    {
        get
        {
            int lx = x - Bounds.Min.X;
            int ly = y - Bounds.Min.Y;
            int lz = z - Bounds.Min.Z;
            CheckBounds(x, y, z, lx, ly, lz);
            return _voxels[(lz * _sizeY + ly) * _sizeX + lx];
        }
        set
        {
            int lx = x - Bounds.Min.X;
            int ly = y - Bounds.Min.Y;
            int lz = z - Bounds.Min.Z;
            CheckBounds(x, y, z, lx, ly, lz);
            _voxels[(lz * _sizeY + ly) * _sizeX + lx] = value;
        }
    }

    /// <summary>
    /// Gets or sets the voxel at <paramref name="coord"/>. The coordinate
    /// must lie within <see cref="Bounds"/>.
    /// </summary>
    public VoxelInfo this[in VoxelCoord coord]
    {
        get => this[coord.X, coord.Y, coord.Z];
        set => this[coord.X, coord.Y, coord.Z] = value;
    }

    private void CheckBounds(int x, int y, int z, int lx, int ly, int lz)
    {
        if ((uint)lx >= (uint)_sizeX || (uint)ly >= (uint)_sizeY || (uint)lz >= (uint)_sizeZ)
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Voxel ({x}, {y}, {z}) is outside the buffer bounds {Bounds}.");
    }
}
