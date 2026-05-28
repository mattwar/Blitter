using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// The result of a closed-form contact query between two 2D collidables.
/// </summary>
/// <remarks>
/// Convention for <see cref="Normal"/>: it points <em>from</em> the
/// second argument of the query <em>toward</em> the first. That is, for
/// <c>a.TryGetContact(b, out c)</c> or <c>tester.TryGetContact(a, b, out c)</c>,
/// <c>c.Normal</c> points from <c>b</c> toward <c>a</c>. A bounce
/// behavior treating <c>a</c> as the moving sprite and <c>b</c> as the
/// barrier can use <c>c.Normal</c> directly as the "out of the surface"
/// direction.
/// </remarks>
public readonly struct HitContact2D
{
    /// <summary>Unit-length contact normal, pointing from the second argument toward the first.</summary>
    public readonly Vector2 Normal;

    /// <summary>World-space contact point, typically on the surface of the second argument.</summary>
    public readonly Vector2 Point;

    /// <summary>Overlap depth in world units. Positive means overlapping; zero means grazing.</summary>
    public readonly float Penetration;

    public HitContact2D(Vector2 normal, Vector2 point, float penetration)
    {
        Normal = normal;
        Point = point;
        Penetration = penetration;
    }

    /// <summary>
    /// Returns the same contact with the normal sign flipped. Used by
    /// pair dispatchers when the argument order is reversed relative
    /// to the canonical implementation of a primitive pair.
    /// </summary>
    public HitContact2D Flipped() => new(-Normal, Point, Penetration);
}
