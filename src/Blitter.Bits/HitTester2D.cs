namespace Blitter.Bits;

/// <summary>
/// Determines if two <see cref="PosedHitShape2D"/>'s or their primitives overlap.
/// </summary>
public abstract class HitTester2D
{
    /// <summary>
    /// Returns true if either shape 'hits' the other.
    /// </summary>
    public bool TestHit(in PosedHitShape2D a, in PosedHitShape2D b) =>
        // should call back on TestHit(ReadOnlySpan<HitPrimitive2D>, in PosedHitShape2D) below
        a.Shape.TestHit(in a, in b, this);

    /// <summary>
    /// Returns true if any of the primitives 'hit' the shape.
    /// </summary>
    public bool TestHit(ReadOnlySpan<HitPrimitive2D> a, in PosedHitShape2D b) =>
        // should call back on TestHit(ReadOnlySpan<HitPrimitive2D>, ReadOnlySpan<HitPrimitive2D>) below
        b.Shape.TestHitWith(in b, a, this);

    /// <summary>
    /// Returns true if any of the primitives in 'a' hit any of the primitives in 'b'.
    /// </summary>
    public abstract bool TestHit(ReadOnlySpan<HitPrimitive2D> a, ReadOnlySpan<HitPrimitive2D> b);
}

/// <summary>
/// A <see cref="HitTester2D"/> that determines if any primitives intersect.
/// </summary>
public sealed class IntersectsHitTester2D : HitTester2D
{
    /// <summary>
    /// Shared instance — the tester holds no state.
    /// </summary>
    public static readonly IntersectsHitTester2D Instance = new();

    private IntersectsHitTester2D() { }

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
