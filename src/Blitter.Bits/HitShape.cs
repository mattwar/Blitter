namespace Blitter.Bits;

/// <summary>
/// Callback for <see cref="HitShape.Visit"/>. Receives the live
/// stack-allocated primitives of a shape; the span is valid only
/// for the duration of the call.
/// </summary>
public delegate void HitShapeVisitor(ReadOnlySpan<HitPrimitive> primitives);

/// <summary>
/// A collision boundary expressed as one or more
/// <see cref="HitPrimitive"/>s. Shapes build their primitives on the
/// stack each dispatch and hand them to a <see cref="Hitter"/>, so
/// even multi-primitive shapes never allocate.
/// </summary>
public abstract class HitShape
{
    /// <summary>
    /// Enclosing circle used for quick reject.
    /// </summary>
    public abstract BoundingCircle BroadCircle { get; }

    /// <summary>
    /// Stage 1:
    /// The shape builds its primitives on the stack and hands them, along with the other shape, to the hitter; 
    /// the hitter then recurses into <paramref name="other"/> to collect its primitives.
    /// </summary>
    public abstract bool TestHit(HitShape other, Hitter hitter);

    /// <summary>
    /// Stage 2: 
    /// The hitter has the other shape's primitives in <paramref name="other"/> and is calling back into this shape
    /// so it can stackalloc its own primitives and finish dispatch.
    /// </summary>
    public abstract bool TestHitWith(ReadOnlySpan<HitPrimitive> other, Hitter hitter);

    /// <summary>
    /// Hands this shape's current primitives to <paramref name="visitor"/>.
    /// Intended for inspection (debug rendering, gizmos, tests); not on
    /// the collision hot path.
    /// </summary>
    public abstract void Visit(HitShapeVisitor visitor);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="a"/> and <paramref name="b"/> overlap. 
    /// Performs a broad-phase circle test first; on hit, runs the full primitive dispatch via
    /// <see cref="IntersectsHitter"/>.
    /// </summary>
    public static bool Intersects(HitShape a, HitShape b)
    {
        if (!a.BroadCircle.Intersects(b.BroadCircle))
            return false;
        return a.TestHit(b, IntersectsHitter.Instance);
    }
}
