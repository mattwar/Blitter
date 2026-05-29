namespace Blitter.Blocks3D;

/// <summary>
/// Lookup from voxel id to <see cref="VoxelType"/>. Ids are arbitrary
/// non-negative integers; id 0 is always <see cref="VoxelType.Air"/>.
/// Add types up front; the palette is not designed to mutate during
/// gameplay.
/// </summary>
public sealed class VoxelPalette
{
    private readonly Dictionary<int, VoxelType> _byId = new();
    private readonly Dictionary<string, VoxelType> _byName = new(StringComparer.Ordinal);

    public VoxelPalette()
    {
        Add(VoxelType.Air);
    }

    /// <summary>The number of registered types (including air).</summary>
    public int Count => _byId.Count;

    /// <summary>
    /// Registers <paramref name="type"/>. Throws if its id or name is
    /// already used.
    /// </summary>
    public VoxelType Add(VoxelType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.Id < 0)
            throw new ArgumentException("Voxel id must be non-negative.", nameof(type));
        if (!_byId.TryAdd(type.Id, type))
            throw new ArgumentException($"Voxel id {type.Id} is already registered.", nameof(type));
        if (!string.IsNullOrEmpty(type.Name) && !_byName.TryAdd(type.Name, type))
        {
            _byId.Remove(type.Id);
            throw new ArgumentException($"Voxel name '{type.Name}' is already registered.", nameof(type));
        }
        return type;
    }

    /// <summary>
    /// Returns the type for <paramref name="id"/>, or
    /// <see cref="VoxelType.Air"/> if no type is registered under that id.
    /// </summary>
    public VoxelType this[int id] =>
        _byId.TryGetValue(id, out var t) ? t : VoxelType.Air;

    /// <summary>Returns the id of the type registered with <paramref name="name"/>.</summary>
    public int IdOf(string name) =>
        _byName.TryGetValue(name, out var t)
            ? t.Id
            : throw new KeyNotFoundException($"No voxel type named '{name}'.");

    /// <summary>True when the cell at <paramref name="id"/> has no geometry or collision.</summary>
    public bool IsAir(int id) => this[id].IsAir;

    /// <summary>True when the cell at <paramref name="id"/> fully occludes its neighbors.</summary>
    public bool IsOpaque(int id) => this[id].IsOpaque;
}
