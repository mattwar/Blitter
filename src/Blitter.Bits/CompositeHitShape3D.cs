using System.Collections.Immutable;
using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// A <see cref="HitShape3D"/> made of one or more sub-shapes. Hit-tests
/// short-circuit on the first sub that overlaps; contact aggregation
/// keeps the deepest contact across subs. Typical use: per-mesh fits
/// for the parts of a multi-mesh model.
/// </summary>
public sealed class CompositeHitShape3D : HitShape3D
{
    private readonly BoundingSphere _localBoundary;
    private readonly int _primitiveCount;

    /// <summary>The sub-shapes that make up this composite.</summary>
    public ImmutableArray<HitShape3D> Shapes { get; }

    public CompositeHitShape3D(ImmutableArray<HitShape3D> shapes)
    {
        if (shapes.IsDefault) throw new ArgumentException("Shapes array is uninitialized.", nameof(shapes));
        Shapes = shapes;

        var union = BoundingSphere.Empty;
        var count = 0;
        foreach (var s in shapes)
        {
            union = union.Encapsulate(s.LocalBoundary);
            count += s.PrimitiveCount;
        }
        _localBoundary = union;
        _primitiveCount = count;
    }

    public CompositeHitShape3D(params HitShape3D[] shapes)
        : this(ImmutableArray.Create(shapes)) { }

    public override BoundingSphere LocalBoundary => _localBoundary;

    public override int PrimitiveCount => _primitiveCount;

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        foreach (var sub in Shapes)
        {
            // Sub-shape broad-phase: skip a sub whose posed bound
            // doesn't reach the other shape's bounding sphere. Cheap
            // sphere-vs-sphere; always worth doing for a composite.
            if (!PosedSubBound(sub, in mine).Intersects(other.BoundingSphere))
                continue;
            if (sub.TestHit(in mine, in other, tester))
                return true;
        }
        return false;
    }

    public override bool TestHit(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester)
    {
        foreach (var sub in Shapes)
        {
            if (sub.TestHit(in mine, in otherPrim, tester))
                return true;
        }
        return false;
    }

    public override bool TryGetContact(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester, out HitContact3D contact)
    {
        bool found = false;
        HitContact3D best = default;
        foreach (var sub in Shapes)
        {
            if (!PosedSubBound(sub, in mine).Intersects(other.BoundingSphere))
                continue;
            if (sub.TryGetContact(in mine, in other, tester, out var c)
                && (!found || c.Penetration > best.Penetration))
            {
                best = c;
                found = true;
            }
        }
        contact = best;
        return found;
    }

    public override bool TryGetContact(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester, out HitContact3D contact)
    {
        bool found = false;
        HitContact3D best = default;
        foreach (var sub in Shapes)
        {
            if (sub.TryGetContact(in mine, in otherPrim, tester, out var c)
                && (!found || c.Penetration > best.Penetration))
            {
                best = c;
                found = true;
            }
        }
        contact = best;
        return found;
    }

    public override void Visit(in Pose3D mine, HitPrimitiveAction3D action)
    {
        foreach (var sub in Shapes)
            sub.Visit(in mine, action);
    }

    private static BoundingSphere PosedSubBound(HitShape3D sub, in Pose3D mine)
    {
        var local = sub.LocalBoundary;
        if (local.IsEmpty)
            return new BoundingSphere(mine.Position, 0f);
        return new BoundingSphere(mine.Transform(local.Center), local.Radius * mine.Scale);
    }
}
