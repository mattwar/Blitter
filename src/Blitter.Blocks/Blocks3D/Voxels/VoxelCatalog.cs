using System.Collections;

namespace Blitter.Blocks3D;

/// <summary>
/// The set of <see cref="VoxelType"/>s a world can hold, keyed by
/// <see cref="VoxelType.Name"/> and kept as an ordered collection.
/// <see cref="VoxelType.Air"/> is always present at index 0. 
/// Build the catalog up front; it is not designed to mutate during gameplay.
/// </summary>
public sealed class VoxelCatalog : IEnumerable<VoxelType>
{
    private readonly List<VoxelType> _types = new();
    private readonly List<string> _names = new();
    private readonly Dictionary<string, VoxelType> _byName = new(StringComparer.Ordinal);

    /// <summary>Creates a catalog pre-populated with <see cref="VoxelType.Air"/> at index 0.</summary>
    public VoxelCatalog()
    {
        _types.Add(VoxelType.Air);
        _names.Add(VoxelType.Air.Name);
        _byName[VoxelType.Air.Name] = VoxelType.Air;
    }

    /// <summary>The number of registered types, including air.</summary>
    public int Count => _types.Count;

    /// <summary>The registered names, in registration order (index 0 is air).</summary>
    public IReadOnlyList<string> Names => _names;

    /// <summary>
    /// Registers <paramref name="type"/> and returns it. Each type must
    /// carry a unique, non-empty <see cref="VoxelType.Name"/> and may
    /// belong to only one catalog.
    /// </summary>
    public VoxelType Add(VoxelType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (string.IsNullOrEmpty(type.Name))
            throw new ArgumentException("Voxel type must have a name.", nameof(type));
        if (type.Owner is not null)
            throw new ArgumentException($"Voxel type '{type.Name}' already belongs to a catalog.", nameof(type));
        if (!_byName.TryAdd(type.Name, type))
            throw new ArgumentException($"Voxel name '{type.Name}' is already registered.", nameof(type));

        type.Id = _types.Count;
        type.Owner = this;
        _types.Add(type);
        _names.Add(type.Name);
        return type;
    }

    /// <summary>The type at <paramref name="index"/> in registration order.</summary>
    public VoxelType this[int index] => _types[index];

    /// <summary>The type registered under <paramref name="name"/>.</summary>
    public VoxelType this[string name] =>
        _byName.TryGetValue(name, out var t)
            ? t
            : throw new KeyNotFoundException($"No voxel type named '{name}'.");

    /// <summary>
    /// The index of <paramref name="type"/> in this catalog. O(1): air is
    /// always index 0, every other type carries its stamped index.
    /// </summary>
    public int IndexOf(VoxelType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (ReferenceEquals(type, VoxelType.Air))
            return 0;
        if (!ReferenceEquals(type.Owner, this))
            throw new ArgumentException($"Voxel type '{type.Name}' does not belong to this catalog.", nameof(type));
        return type.Id;
    }

    /// <summary>True when a type named <paramref name="name"/> is registered.</summary>
    public bool Contains(string name) => _byName.ContainsKey(name);

    /// <summary>Looks up the type registered under <paramref name="name"/>.</summary>
    public bool TryGet(string name, out VoxelType type) => _byName.TryGetValue(name, out type!);

    /// <summary>Looks up the index of the type registered under <paramref name="name"/>.</summary>
    public bool TryGetIndex(string name, out int index)
    {
        if (_byName.TryGetValue(name, out var t))
        {
            index = IndexOf(t);
            return true;
        }
        index = 0;
        return false;
    }

    /// <summary>Enumerates the registered types in registration order.</summary>
    public IEnumerator<VoxelType> GetEnumerator() => _types.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
