using System.Collections.Immutable;

namespace Blitter.Bits;

/// <summary>
/// An ordered, named collection of <see cref="AnimationSequence"/>'s.
/// </summary>
public sealed class AnimationCatalog
{
    private readonly Dictionary<string, AnimationSequence> _byName;
    private readonly ImmutableArray<AnimationSequence> _sequences;
    private readonly ImmutableArray<string> _names;

    /// <summary>
    /// Constructs a catalog from a set of named sequences. Names must be unique.
    /// </summary>
    public AnimationCatalog(IEnumerable<KeyValuePair<string, AnimationSequence>> sequences)
    {
        ArgumentNullException.ThrowIfNull(sequences);

        var built = ImmutableArray.CreateBuilder<AnimationSequence>();
        _byName = new Dictionary<string, AnimationSequence>(StringComparer.Ordinal);
        var names = ImmutableArray.CreateBuilder<string>();

        foreach (var kv in sequences)
        {
            ArgumentException.ThrowIfNullOrEmpty(kv.Key);
            ArgumentNullException.ThrowIfNull(kv.Value);
            if (!_byName.TryAdd(kv.Key, kv.Value))
                throw new ArgumentException($"Duplicate sequence name '{kv.Key}'.", nameof(sequences));
            built.Add(kv.Value);
            names.Add(kv.Key);
        }

        if (built.Count == 0)
            throw new ArgumentException("At least one sequence is required.", nameof(sequences));

        _sequences = built.ToImmutable();
        _names = names.ToImmutable();
    }

    /// <summary>Number of sequences in the catalog.</summary>
    public int Count => _sequences.Length;

    /// <summary>Sequences in declaration order.</summary>
    public ImmutableArray<AnimationSequence> Sequences => _sequences;

    /// <summary>Sequence names in declaration order.</summary>
    public ImmutableArray<string> Names => _names;

    /// <summary>Looks up a sequence by name.</summary>
    public AnimationSequence this[string name] => _byName[name];

    /// <summary>Looks up a sequence by declaration index.</summary>
    public AnimationSequence this[int index] => _sequences[index];

    /// <summary>True if a sequence with the given name is registered.</summary>
    public bool Contains(string name) => _byName.ContainsKey(name);

    /// <summary>Try-pattern lookup by name.</summary>
    public bool TryGet(string name, out AnimationSequence sequence) =>
        _byName.TryGetValue(name, out sequence!);
}
