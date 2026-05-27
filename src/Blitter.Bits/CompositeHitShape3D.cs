using System.Collections.Immutable;
using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// A <see cref="HitShape3D"/> made of one or more sub-shapes. Hit-tests
/// short-circuit on the first sub that overlaps; primitive enumeration
/// concatenates every sub's primitives. Typical use: per-mesh fits for
/// the parts of a multi-mesh model.
/// </summary>
public sealed class CompositeHitShape3D : HitShape3D
{
    private readonly BoundingSphere _localBoundary;

    /// <summary>The sub-shapes that make up this composite.</summary>
    public ImmutableArray<HitShape3D> Shapes { get; }

    public CompositeHitShape3D(ImmutableArray<HitShape3D> shapes)
    {
        if (shapes.IsDefault) throw new ArgumentException("Shapes array is uninitialized.", nameof(shapes));
        Shapes = shapes;

        var union = BoundingSphere.Empty;
        foreach (var s in shapes)
            union = union.Encapsulate(s.LocalBoundary);
        _localBoundary = union;
    }

    public CompositeHitShape3D(params HitShape3D[] shapes)
        : this(ImmutableArray.Create(shapes)) { }

    public override BoundingSphere LocalBoundary => _localBoundary;

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        foreach (var sub in Shapes)
        {
            if (sub.TestHit(in mine, in other, tester))
                return true;
        }
        return false;
    }

    public override bool TestHitWith(in Pose3D mine, ReadOnlySpan<HitPrimitive3D> other, HitTester3D tester)
    {
        foreach (var sub in Shapes)
        {
            if (sub.TestHitWith(in mine, other, tester))
                return true;
        }
        return false;
    }

    public override void Visit(in Pose3D mine, HitShapeVisitor3D visitor)
    {
        // Each sub hands its own posed primitives to the visitor in turn.
        // Callers see the concatenation across all subs.
        foreach (var sub in Shapes)
            sub.Visit(in mine, visitor);
    }

    public override HitShape3D Translate(Vector3 offset)
    {
        var builder = ImmutableArray.CreateBuilder<HitShape3D>(Shapes.Length);
        foreach (var s in Shapes)
            builder.Add(s.Translate(offset));
        return new CompositeHitShape3D(builder.ToImmutable());
    }
}
