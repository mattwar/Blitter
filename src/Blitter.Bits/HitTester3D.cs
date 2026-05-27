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

/// <summary>
/// Computes the deepest contact (largest <see cref="HitContact3D.Penetration"/>)
/// between two <see cref="PosedHitShape3D"/>s or their primitives, when
/// a primitive pair has a closed-form contact resolution.
/// </summary>
public sealed class ContactHitTester3D
{
    /// <summary>Shared instance — the tester holds no state.</summary>
    public static readonly ContactHitTester3D Instance = new();

    private ContactHitTester3D() { }

    /// <summary>
    /// True when <paramref name="a"/> and <paramref name="b"/> overlap;
    /// <paramref name="contact"/> reports the deepest contact found.
    /// Convention: <see cref="HitContact3D.Normal"/> points from
    /// <paramref name="b"/> toward <paramref name="a"/>.
    /// </summary>
    public bool TryGetContact(in PosedHitShape3D a, in PosedHitShape3D b, out HitContact3D contact)
    {
        if (!a.BoundingSphere.Intersects(b.BoundingSphere))
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
    public bool TryGetContact(ReadOnlySpan<HitPrimitive3D> a, in PosedHitShape3D b, out HitContact3D contact) =>
        b.Shape.TryGetContactWith(in b.Pose, a, this, out contact);

    /// <summary>
    /// Walks every primitive pair and keeps the deepest contact found.
    /// Per-pair convention: <see cref="HitContact3D.Normal"/> points
    /// from <paramref name="b"/>[j] toward <paramref name="a"/>[i].
    /// </summary>
    public bool TryGetContact(ReadOnlySpan<HitPrimitive3D> a, ReadOnlySpan<HitPrimitive3D> b, out HitContact3D contact)
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
