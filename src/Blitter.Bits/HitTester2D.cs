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
        a.Shape.TestHit(in a.Pose, in b, this);

    /// <summary>
    /// Returns true if any of the primitives 'hit' the shape.
    /// </summary>
    public bool TestHit(ReadOnlySpan<HitPrimitive2D> a, in PosedHitShape2D b) =>
        // should call back on TestHit(ReadOnlySpan<HitPrimitive2D>, ReadOnlySpan<HitPrimitive2D>) below
        b.Shape.TestHitWith(in b.Pose, a, this);

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

/// <summary>
/// Computes the deepest contact (largest <see cref="HitContact2D.Penetration"/>)
/// between two <see cref="PosedHitShape2D"/>s or their primitives, when
/// a primitive pair has a closed-form contact resolution.
/// </summary>
public sealed class ContactHitTester2D
{
    /// <summary>Shared instance — the tester holds no state.</summary>
    public static readonly ContactHitTester2D Instance = new();

    private ContactHitTester2D() { }

    /// <summary>
    /// True when <paramref name="a"/> and <paramref name="b"/> overlap;
    /// <paramref name="contact"/> reports the deepest contact found.
    /// Convention: <see cref="HitContact2D.Normal"/> points from
    /// <paramref name="b"/> toward <paramref name="a"/>.
    /// </summary>
    public bool TryGetContact(in PosedHitShape2D a, in PosedHitShape2D b, out HitContact2D contact)
    {
        if (!a.BoundingCircle.Intersects(b.BoundingCircle))
        {
            contact = default;
            return false;
        }
        return a.Shape.TryGetContact(in a.Pose, in b, this, out contact);
    }

    /// <summary>
    /// True when any primitive in <paramref name="a"/> contacts
    /// <paramref name="b"/>'s shape. Convention: normal points from
    /// <paramref name="b"/> toward <paramref name="a"/>.
    /// </summary>
    public bool TryGetContact(ReadOnlySpan<HitPrimitive2D> a, in PosedHitShape2D b, out HitContact2D contact) =>
        b.Shape.TryGetContactWith(in b.Pose, a, this, out contact);

    /// <summary>
    /// Walks every primitive pair and keeps the deepest contact found.
    /// Per-pair convention: <see cref="HitContact2D.Normal"/> points
    /// from <paramref name="b"/>[j] toward <paramref name="a"/>[i].
    /// </summary>
    public bool TryGetContact(ReadOnlySpan<HitPrimitive2D> a, ReadOnlySpan<HitPrimitive2D> b, out HitContact2D contact)
    {
        bool found = false;
        contact = default;
        for (int i = 0; i < a.Length; i++)
        {
            for (int j = 0; j < b.Length; j++)
            {
                if (a[i].TryGetContact(in b[j], out var c)
                    && (!found || c.Penetration > contact.Penetration))
                {
                    contact = c;
                    found = true;
                }
            }
        }
        return found;
    }
}
