using System.Collections.Immutable;

namespace Blitter.Bits;

/// <summary>
/// An <see cref="Atlas"/> together with a set of named <see cref="AnimationSequence"/>s that index into it. 
/// Reusable data; a single instance can back many <see cref="AnimatedVisual2D"/>s.
/// </summary>
public sealed class AnimationAtlas
{
    private readonly Dictionary<string, AnimationSequence> _byName;
    private readonly ImmutableArray<AnimationSequence> _sequences;
    private readonly ImmutableArray<string> _states;

    public AnimationAtlas(
        Atlas atlas,
        IEnumerable<AnimationSequence> sequences,
        string? defaultState = null)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(sequences);

        Atlas = atlas;
        var list = sequences.ToImmutableArray();
        if (list.Length == 0)
            throw new ArgumentException("At least one sequence is required.", nameof(sequences));

        _byName = new Dictionary<string, AnimationSequence>(list.Length, StringComparer.Ordinal);
        var names = ImmutableArray.CreateBuilder<string>(list.Length);
        foreach (var seq in list)
        {
            ArgumentNullException.ThrowIfNull(seq);
            // Validate every frame index against the atlas before anyone consumes it.
            foreach (var frame in seq.Frames)
            {
                if ((uint)frame >= (uint)atlas.Count)
                    throw new ArgumentOutOfRangeException(nameof(sequences),
                        $"Sequence '{seq.Name}' references frame {frame} outside the atlas range [0, {atlas.Count}).");
            }
            if (!_byName.TryAdd(seq.Name, seq))
                throw new ArgumentException($"Duplicate sequence name '{seq.Name}'.", nameof(sequences));
            names.Add(seq.Name);
        }

        _sequences = list;
        _states = names.ToImmutable();

        if (defaultState is null)
        {
            DefaultState = list[0].Name;
        }
        else
        {
            if (!_byName.ContainsKey(defaultState))
                throw new ArgumentException($"Default state '{defaultState}' is not one of the sequences.", nameof(defaultState));
            DefaultState = defaultState;
        }
    }

    /// <summary>Backing atlas providing the source regions.</summary>
    public Atlas Atlas { get; }

    /// <summary>Sequences in declaration order.</summary>
    public ImmutableArray<AnimationSequence> Sequences => _sequences;

    /// <summary>Sequence names in declaration order.</summary>
    public ImmutableArray<string> States => _states;

    /// <summary>Name of the sequence used when no other state is selected.</summary>
    public string DefaultState { get; }

    /// <summary>Looks up a sequence by name.</summary>
    public AnimationSequence this[string name] => _byName[name];

    /// <summary>Looks up a sequence by declaration index.</summary>
    public AnimationSequence this[int index] => _sequences[index];

    /// <summary>Try-pattern lookup by name.</summary>
    public bool TryGet(string name, out AnimationSequence sequence) =>
        _byName.TryGetValue(name, out sequence!);

    /// <summary>
    /// Convenience factory for the single-sequence case: builds an
    /// <see cref="AnimationAtlas"/> from a raw atlas with one default
    /// sequence covering the given (or all) frames.
    /// </summary>
    public static AnimationAtlas Single(
        Atlas atlas,
        TimeSpan frameDuration,
        AnimationLoop loop = AnimationLoop.Loop,
        ReadOnlySpan<int> frames = default,
        string name = "default")
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ImmutableArray<int> ix;
        if (frames.IsEmpty)
        {
            if (atlas.Count == 0)
                throw new ArgumentException("Atlas has no frames.", nameof(atlas));
            var builder = ImmutableArray.CreateBuilder<int>(atlas.Count);
            for (int i = 0; i < atlas.Count; i++) builder.Add(i);
            ix = builder.MoveToImmutable();
        }
        else
        {
            ix = ImmutableArray.Create(frames);
        }
        return new AnimationAtlas(atlas, [new AnimationSequence(name, ix, frameDuration, loop)]);
    }
}
