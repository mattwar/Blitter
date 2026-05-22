namespace Blitter.Bits;

/// <summary>
/// Receives both sides of a 2D collision query and decides what counts
/// as a hit. Abstract class (not interface) so the engine pays a
/// single direct vtable lookup per dispatch rather than going
/// through the interface dispatch table.
/// </summary>
/// <remarks>
/// The double-dispatch protocol against <see cref="PosedHitShape2D"/>:
/// stage 1 hands us shape A's posed primitives and B (still a posed
/// shape); we expand B by stackallocing its primitives; stage 2 fires
/// with both spans live on the stack, where subclasses do the real work.
/// </remarks>
public abstract class Hitter2D
{
    /// <summary>
    /// Stage 1: shape A has placed its posed primitives in
    /// <paramref name="a"/>. Default behavior recurses into
    /// <paramref name="b"/> so it can stackalloc its own primitives
    /// and finish dispatch via the span-vs-span stage. Subclasses
    /// rarely need to override this stage.
    /// </summary>
    public virtual bool TestHit(ReadOnlySpan<HitPrimitive2D> a, in PosedHitShape2D b) =>
        b.Shape.TestHitWith(in b, a, this);

    /// <summary>
    /// Stage 2: both primitive lists are live on the stack. Subclasses
    /// implement their pairwise logic here.
    /// </summary>
    public abstract bool TestHit(ReadOnlySpan<HitPrimitive2D> a, ReadOnlySpan<HitPrimitive2D> b);
}

/// <summary>
/// Default hitter: returns <c>true</c> on the first intersecting
/// primitive pair. Stateless; use <see cref="Instance"/>.
/// </summary>
public sealed class IntersectsHitter2D : Hitter2D
{
    /// <summary>
    /// Shared instance — the hitter holds no state.
    /// </summary>
    public static readonly IntersectsHitter2D Instance = new();

    private IntersectsHitter2D() { }

    /// <inheritdoc/>
    public override bool TestHit(ReadOnlySpan<HitPrimitive2D> a, ReadOnlySpan<HitPrimitive2D> b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            for (int j = 0; j < b.Length; j++)
            {
                if (a[i].Intersects(b[j]))
                    return true;
            }
        }
        return false;
    }
}
