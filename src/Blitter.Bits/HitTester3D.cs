namespace Blitter.Bits;

/// <summary>
/// Strategy object that resolves primitive-vs-primitive intersection
/// and contact between two <see cref="HitPrimitive3D"/> values.
/// Shapes own iteration; the tester owns the math.
/// </summary>
public class HitTester3D
{
    /// <summary>Shared default tester using stock primitive math.</summary>
    public static HitTester3D Default { get; } = new();

    /// <summary>True when primitives <paramref name="a"/> and <paramref name="b"/> overlap.</summary>
    public virtual bool TestHit(in HitPrimitive3D a, in HitPrimitive3D b) =>
        a.Intersects(in b);

    /// <summary>
    /// Computes the closed-form contact between <paramref name="a"/>
    /// and <paramref name="b"/>. Normal points from <paramref name="b"/>
    /// toward <paramref name="a"/>.
    /// </summary>
    public virtual bool TryGetContact(in HitPrimitive3D a, in HitPrimitive3D b, out HitContact3D contact) =>
        a.TryGetContact(in b, out contact);
}
