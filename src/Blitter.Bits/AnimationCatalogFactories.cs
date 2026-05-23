using System.Collections.Immutable;

namespace Blitter.Bits;

/// <summary>
/// Extension methods that build <see cref="AnimationCatalog"/>s from
/// an <see cref="TextureCatalog"/>'s frames.
/// </summary>
public static class AnimationCatalogFactories
{
    /// <summary>A description of one sequence to build from atlas frames.</summary>
    public sealed record Spec(
        string Name,
        ImmutableArray<int> Frames,
        TimeSpan FrameDuration,
        AnimationLoop Loop = AnimationLoop.Loop);

    /// <summary>
    /// Builds an <see cref="AnimationCatalog"/> by materializing
    /// <paramref name="specs"/> against the frames of <paramref name="atlas"/>.
    /// </summary>
    public static AnimationCatalog ToAnimationCatalog(
        this TextureCatalog atlas,
        IEnumerable<Spec> specs)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(specs);

        var seqs = new List<KeyValuePair<string, AnimationSequence>>();
        foreach (var spec in specs)
        {
            ArgumentNullException.ThrowIfNull(spec);
            if (spec.Frames.IsDefaultOrEmpty)
                throw new ArgumentException(
                    $"Sequence '{spec.Name}' has no frames.", nameof(specs));

            var frames = ImmutableArray.CreateBuilder<Texture2D>(spec.Frames.Length);
            foreach (var i in spec.Frames)
            {
                if ((uint)i >= (uint)atlas.Count)
                    throw new ArgumentOutOfRangeException(nameof(specs),
                        $"Sequence '{spec.Name}' references frame {i} outside the atlas range [0, {atlas.Count}).");
                frames.Add(atlas[i]);
            }

            seqs.Add(new KeyValuePair<string, AnimationSequence>(
                spec.Name,
                new AnimationSequence(frames.MoveToImmutable(), spec.FrameDuration, spec.Loop)));
        }

        return new AnimationCatalog(seqs);
    }

    /// <summary>
    /// Builds an <see cref="AnimationCatalog"/> containing a single sequence
    /// over the given (or all) atlas frames.
    /// </summary>
    public static AnimationCatalog ToSingleSequenceCatalog(
        this TextureCatalog atlas,
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
                throw new ArgumentException("TextureCatalog has no frames.", nameof(atlas));
            var builder = ImmutableArray.CreateBuilder<int>(atlas.Count);
            for (int i = 0; i < atlas.Count; i++) builder.Add(i);
            ix = builder.MoveToImmutable();
        }
        else
        {
            ix = ImmutableArray.Create(frames);
        }
        return atlas.ToAnimationCatalog([new Spec(name, ix, frameDuration, loop)]);
    }
}
