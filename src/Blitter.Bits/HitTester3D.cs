namespace Blitter.Bits;

/// <summary>
/// Determines if two <see cref="PosedHitShape3D"/>s or their primitives overlap.
/// </summary>
public abstract class HitTester3D
{
    /// <summary>Returns true if either shape 'hits' the other.</summary>
    public bool TestHit(in PosedHitShape3D a, in PosedHitShape3D b) =>
        a.Shape.TestHit(in a.Pose, in b, this);

    /// <summary>Returns true if any of the primitives 'hit' the shape.</summary>
    public bool TestHit(ReadOnlySpan<HitPrimitive3D> a, in PosedHitShape3D b) =>
        b.Shape.TestHitWith(in b.Pose, a, this);

    /// <summary>Returns true if any of the primitives in 'a' hit any of the primitives in 'b'.</summary>
    public abstract bool TestHit(ReadOnlySpan<HitPrimitive3D> a, ReadOnlySpan<HitPrimitive3D> b);
}

/// <summary>
/// A <see cref="HitTester3D"/> that determines if any primitives intersect.
/// </summary>
public sealed class IntersectsHitTester3D : HitTester3D
{
    /// <summary>Shared instance — the tester holds no state.</summary>
    public static readonly IntersectsHitTester3D Instance = new();

    private IntersectsHitTester3D() { }

    /// <inheritdoc/>
    public override bool TestHit(ReadOnlySpan<HitPrimitive3D> a, ReadOnlySpan<HitPrimitive3D> b)
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
