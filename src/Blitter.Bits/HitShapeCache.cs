using System.Numerics;
using System.Runtime.CompilerServices;

namespace Blitter.Bits;

/// <summary>
/// Caches a <see cref="HitShape2D"/> per <see cref="Texture2D"/>, sharing
/// computed shapes across all visuals that use the same cache instance.
/// Subclass and override <see cref="ComputeHitShape"/> to change how
/// shapes are derived; pass the subclass instance to a visual's
/// constructor to use it.
/// </summary>
public class HitShapeCache
{
    /// <summary>Process-wide default cache used by visuals when none is supplied.</summary>
    public static HitShapeCache Default { get; } = new();

    private readonly ConditionalWeakTable<Texture2D, HitShape2D> _shapes = new();

    // cached callback so we don't allocate a new delegate each time.
    private readonly ConditionalWeakTable<Texture2D, HitShape2D>.CreateValueCallback _computeCallback;

    public HitShapeCache()
    {
        _computeCallback = ComputeHitShape;
    }

    /// <summary>
    /// Gets the cached <see cref="HitShape2D"/> or creates one for the <paramref name="texture"/>.
    /// </summary>
    public HitShape2D GetOrCreateHitShape(Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return _shapes.GetValue(texture, _computeCallback);
    }

    /// <summary>
    /// Computes a hit shape from <paramref name="texture"/>. 
    /// The default behavior uses ComputeOpaqueHitShape2D for readable textures and a bounding circle for non-readable ones.
    /// </summary>
    protected virtual HitShape2D ComputeHitShape(Texture2D texture)
    {
        var size = texture.Size;
        if (texture is ReadableTexture2D readable)
        {
            return readable
                .ComputeOpaqueHitShape2D()
                .Translate(new Vector2(-size.Width / 2f, -size.Height / 2f));
        }
        
        var half = new Vector2(size.Width / 2f, size.Height / 2f);
        return new CircleHitShape2D(Vector2.Zero, half.Length());
    }
}
