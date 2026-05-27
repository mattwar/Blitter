namespace Blitter.Bits;

/// <summary>
/// Strategy object that resolves primitive-vs-primitive intersection
/// and contact between two <see cref="HitPrimitive2D"/> values.
/// Shapes own iteration; the tester owns the math.
/// </summary>
public class HitTester2D
{
    /// <summary>Shared default tester using stock primitive math.</summary>
    public static HitTester2D Default { get; } = new();

    /// <summary>True when primitives <paramref name="a"/> and <paramref name="b"/> overlap.</summary>
    public virtual bool TestHit(in HitPrimitive2D a, in HitPrimitive2D b) =>
        a.Intersects(in b);

    /// <summary>
    /// Computes the closed-form contact between <paramref name="a"/>
    /// and <paramref name="b"/>. Normal points from <paramref name="b"/>
    /// toward <paramref name="a"/>.
    /// </summary>
    public virtual bool TryGetContact(in HitPrimitive2D a, in HitPrimitive2D b, out HitContact2D contact) =>
        a.TryGetContact(in b, out contact);
}
