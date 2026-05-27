using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// A delegate that receives one posed primitive of a <see cref="HitShape3D"/>.
/// </summary>
public delegate void HitPrimitiveAction3D(in HitPrimitive3D primitive);

/// <summary>
/// An abstraction of a 3D collision boundary, in local (model) coordinates
/// (origin at the model's local origin, unrotated, unscaled). The 3D
/// analog of <see cref="HitShape2D"/>.
/// </summary>
/// <remarks>
/// Hit-testing is callback-driven and allocation-free: each shape walks
/// its own primitives one at a time, delegating each to the other
/// shape's primitive overload or, at the leaves, to a
/// <see cref="HitTester3D"/> that runs the primitive-vs-primitive math.
/// </remarks>
public abstract class HitShape3D
{
    /// <summary>Local-space bounding sphere used for the broad-phase reject.</summary>
    public abstract BoundingSphere LocalBoundary { get; }

    /// <summary>
    /// How many primitives this shape emits per hit-test pass. Compound
    /// shapes (mesh, composite) use this on the <em>other</em> shape
    /// to decide whether a per-primitive broad-phase prune is
    /// worthwhile: pruning only pays off when the other side has more
    /// than one primitive to iterate.
    /// </summary>
    public abstract int PrimitiveCount { get; }

    /// <summary>
    /// True when this shape, posed by <paramref name="mine"/>, hits
    /// the posed shape <paramref name="other"/>. Implementations walk
    /// their own primitives and delegate each to <paramref name="other"/>'s
    /// primitive overload.
    /// </summary>
    public abstract bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester);

    /// <summary>
    /// True when this shape, posed by <paramref name="mine"/>, hits
    /// the single primitive <paramref name="otherPrim"/>. Implementations
    /// walk their own primitives and call <paramref name="tester"/> for
    /// each pair.
    /// </summary>
    public abstract bool TestHit(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester);

    /// <summary>
    /// Computes the deepest closed-form contact between this shape
    /// (posed by <paramref name="mine"/>) and <paramref name="other"/>.
    /// Convention: <see cref="HitContact3D.Normal"/> points from
    /// <paramref name="other"/> toward this shape.
    /// </summary>
    public abstract bool TryGetContact(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester, out HitContact3D contact);

    /// <summary>
    /// Computes the deepest closed-form contact between this shape
    /// (posed by <paramref name="mine"/>) and the single primitive
    /// <paramref name="otherPrim"/>. Convention: normal points from
    /// <paramref name="otherPrim"/> toward this shape.
    /// </summary>
    public abstract bool TryGetContact(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester, out HitContact3D contact);

    /// <summary>
    /// Hands every posed primitive of this shape to
    /// <paramref name="action"/> one at a time. Off the collision hot
    /// path (debug rendering, gizmos, tests).
    /// </summary>
    public abstract void Visit(in Pose3D mine, HitPrimitiveAction3D action);

    /// <summary>Copies this <see cref="HitShape3D"/> with the center offset by <paramref name="offset"/>.</summary>
    public abstract HitShape3D Translate(Vector3 offset);

    /// <summary>Shared "no shape" sentinel — never hits anything.</summary>
    public static readonly HitShape3D None = new NoneShape();

    private sealed class NoneShape : HitShape3D
    {
        public override BoundingSphere LocalBoundary => BoundingSphere.Empty;
        public override int PrimitiveCount => 0;
        public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester) => false;
        public override bool TestHit(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester) => false;
        public override bool TryGetContact(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester, out HitContact3D contact)
        {
            contact = default;
            return false;
        }
        public override bool TryGetContact(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester, out HitContact3D contact)
        {
            contact = default;
            return false;
        }
        public override void Visit(in Pose3D mine, HitPrimitiveAction3D action) { }
        public override HitShape3D Translate(Vector3 offset) => this;
    }
}

/// <summary>
/// A <see cref="HitShape3D"/> combined with a <see cref="Pose3D"/>
/// (world-space position, orientation, scale).
/// </summary>
public readonly struct PosedHitShape3D
{
    /// <summary>The local-space shape.</summary>
    public readonly HitShape3D Shape;

    /// <summary>World-space pose applied to the local geometry.</summary>
    public readonly Pose3D Pose;

    public PosedHitShape3D(HitShape3D shape, Pose3D pose)
    {
        Shape = shape;
        Pose = pose;
    }

    public PosedHitShape3D(HitShape3D shape, Vector3 position, Quaternion rotation, float scale = 1f)
        : this(shape, new Pose3D(position, rotation, scale)) { }

    /// <summary>
    /// World-space broad-phase sphere, computed from <see cref="HitShape3D.LocalBoundary"/>
    /// and the current pose.
    /// </summary>
    public BoundingSphere BoundingSphere
    {
        get
        {
            var local = Shape.LocalBoundary;
            if (local.IsEmpty)
                return new BoundingSphere(Pose.Position, 0f);
            return new BoundingSphere(Pose.Transform(local.Center), local.Radius * Pose.Scale);
        }
    }

    /// <summary>True when this posed shape hits <paramref name="other"/> using <see cref="HitTester3D.Default"/>.</summary>
    public bool TestHit(in PosedHitShape3D other) =>
        TestHit(in other, HitTester3D.Default);

    /// <summary>True when this posed shape hits <paramref name="other"/>.</summary>
    public bool TestHit(in PosedHitShape3D other, HitTester3D tester)
    {
        if (!BoundingSphere.Intersects(other.BoundingSphere))
            return false;
        return Shape.TestHit(in Pose, in other, tester);
    }

    /// <summary>
    /// Reports the deepest contact between this posed shape and
    /// <paramref name="other"/> using <see cref="HitTester3D.Default"/>.
    /// </summary>
    public bool TryGetContact(in PosedHitShape3D other, out HitContact3D contact) =>
        TryGetContact(in other, HitTester3D.Default, out contact);

    /// <summary>
    /// Reports the deepest contact between this posed shape and
    /// <paramref name="other"/>. Normal points from
    /// <paramref name="other"/> toward this shape.
    /// </summary>
    public bool TryGetContact(in PosedHitShape3D other, HitTester3D tester, out HitContact3D contact)
    {
        if (!BoundingSphere.Intersects(other.BoundingSphere))
        {
            contact = default;
            return false;
        }
        return Shape.TryGetContact(in Pose, in other, tester, out contact);
    }

    /// <summary>Hands this shape's current posed primitives to <paramref name="action"/>, one at a time.</summary>
    public void Visit(HitPrimitiveAction3D action) =>
        Shape.Visit(in Pose, action);
}

/// <summary>
/// A <see cref="HitShape3D"/> that is a single sphere. The center and
/// radius are in local (model) coordinates.
/// </summary>
public sealed class SphereHitShape3D : HitShape3D
{
    public Vector3 LocalCenter { get; }
    public float LocalRadius { get; }

    public SphereHitShape3D(Vector3 localCenter, float localRadius)
    {
        LocalCenter = localCenter;
        LocalRadius = localRadius;
    }

    public override BoundingSphere LocalBoundary => new(LocalCenter, LocalRadius);

    public override int PrimitiveCount => 1;

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        var p = Pose(in mine);
        return other.Shape.TestHit(in other.Pose, in p, tester);
    }

    public override bool TestHit(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester)
    {
        var p = Pose(in mine);
        return tester.TestHit(in p, in otherPrim);
    }

    public override bool TryGetContact(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester, out HitContact3D contact)
    {
        // other.TryGetContact(myPrim) returns "from myPrim → other".
        // Outer convention is "from other → me", so flip.
        var p = Pose(in mine);
        if (other.Shape.TryGetContact(in other.Pose, in p, tester, out contact))
        {
            contact = contact.Flipped();
            return true;
        }
        return false;
    }

    public override bool TryGetContact(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester, out HitContact3D contact)
    {
        // tester.TryGetContact(a=mine, b=otherPrim) returns "from b → a"
        // = "from otherPrim → me". Matches receiver convention.
        var p = Pose(in mine);
        return tester.TryGetContact(in p, in otherPrim, out contact);
    }

    public override void Visit(in Pose3D mine, HitPrimitiveAction3D action) =>
        action(Pose(in mine));

    private HitPrimitive3D Pose(in Pose3D pose) =>
        HitPrimitive3D.Sphere(pose.Transform(LocalCenter), LocalRadius * pose.Scale);

    public override HitShape3D Translate(Vector3 offset) =>
        new SphereHitShape3D(LocalCenter + offset, LocalRadius);
}

/// <summary>
/// A <see cref="HitShape3D"/> that is a capsule: the Minkowski sum of
/// segment <see cref="LocalEndA"/>–<see cref="LocalEndB"/> with a ball
/// of <see cref="LocalRadius"/>. Endpoints are in local (model) coordinates.
/// </summary>
public sealed class CapsuleHitShape3D : HitShape3D
{
    public Vector3 LocalEndA { get; }
    public Vector3 LocalEndB { get; }
    public float LocalRadius { get; }

    public CapsuleHitShape3D(Vector3 localEndA, Vector3 localEndB, float localRadius)
    {
        LocalEndA = localEndA;
        LocalEndB = localEndB;
        LocalRadius = localRadius;
    }

    public override BoundingSphere LocalBoundary
    {
        get
        {
            var mid = (LocalEndA + LocalEndB) * 0.5f;
            var half = (LocalEndA - LocalEndB).Length() * 0.5f;
            return new BoundingSphere(mid, half + LocalRadius);
        }
    }

    public override int PrimitiveCount => 1;

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        var p = Pose(in mine);
        return other.Shape.TestHit(in other.Pose, in p, tester);
    }

    public override bool TestHit(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester)
    {
        var p = Pose(in mine);
        return tester.TestHit(in p, in otherPrim);
    }

    public override bool TryGetContact(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester, out HitContact3D contact)
    {
        var p = Pose(in mine);
        if (other.Shape.TryGetContact(in other.Pose, in p, tester, out contact))
        {
            contact = contact.Flipped();
            return true;
        }
        return false;
    }

    public override bool TryGetContact(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester, out HitContact3D contact)
    {
        var p = Pose(in mine);
        return tester.TryGetContact(in p, in otherPrim, out contact);
    }

    public override void Visit(in Pose3D mine, HitPrimitiveAction3D action) =>
        action(Pose(in mine));

    private HitPrimitive3D Pose(in Pose3D pose) =>
        HitPrimitive3D.Capsule(
            pose.Transform(LocalEndA),
            pose.Transform(LocalEndB),
            LocalRadius * pose.Scale);

    public override HitShape3D Translate(Vector3 offset) =>
        new CapsuleHitShape3D(LocalEndA + offset, LocalEndB + offset, LocalRadius);
}

/// <summary>
/// A <see cref="HitShape3D"/> that is a solid right cylinder with flat
/// caps. The axis runs from <see cref="LocalBase"/> to
/// <see cref="LocalTop"/> in local (model) coordinates.
/// </summary>
public sealed class CylinderHitShape3D : HitShape3D
{
    public Vector3 LocalBase { get; }
    public Vector3 LocalTop { get; }
    public float LocalRadius { get; }

    public CylinderHitShape3D(Vector3 localBase, Vector3 localTop, float localRadius)
    {
        LocalBase = localBase;
        LocalTop = localTop;
        LocalRadius = localRadius;
    }

    public override BoundingSphere LocalBoundary
    {
        get
        {
            var mid = (LocalBase + LocalTop) * 0.5f;
            var half = (LocalTop - LocalBase).Length() * 0.5f;
            return new BoundingSphere(mid, half + LocalRadius);
        }
    }

    public override int PrimitiveCount => 1;

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        var p = Pose(in mine);
        return other.Shape.TestHit(in other.Pose, in p, tester);
    }

    public override bool TestHit(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester)
    {
        var p = Pose(in mine);
        return tester.TestHit(in p, in otherPrim);
    }

    public override bool TryGetContact(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester, out HitContact3D contact)
    {
        var p = Pose(in mine);
        if (other.Shape.TryGetContact(in other.Pose, in p, tester, out contact))
        {
            contact = contact.Flipped();
            return true;
        }
        return false;
    }

    public override bool TryGetContact(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester, out HitContact3D contact)
    {
        var p = Pose(in mine);
        return tester.TryGetContact(in p, in otherPrim, out contact);
    }

    public override void Visit(in Pose3D mine, HitPrimitiveAction3D action) =>
        action(Pose(in mine));

    private HitPrimitive3D Pose(in Pose3D pose) =>
        HitPrimitive3D.Cylinder(
            pose.Transform(LocalBase),
            pose.Transform(LocalTop),
            LocalRadius * pose.Scale);

    public override HitShape3D Translate(Vector3 offset) =>
        new CylinderHitShape3D(LocalBase + offset, LocalTop + offset, LocalRadius);
}

/// <summary>
/// A <see cref="HitShape3D"/> that is a solid oriented box.
/// <see cref="LocalHalfExtents"/> are the half-widths along the box's
/// local X / Y / Z axes; <see cref="LocalRotation"/> rotates those
/// axes within the model's frame (the pose's rotation rotates the
/// model on top).
/// </summary>
public sealed class BoxHitShape3D : HitShape3D
{
    public Vector3 LocalCenter { get; }
    public Vector3 LocalHalfExtents { get; }
    public Quaternion LocalRotation { get; }

    public BoxHitShape3D(Vector3 localCenter, Vector3 localHalfExtents)
        : this(localCenter, localHalfExtents, Quaternion.Identity) { }

    public BoxHitShape3D(Vector3 localCenter, Vector3 localHalfExtents, Quaternion localRotation)
    {
        LocalCenter = localCenter;
        LocalHalfExtents = localHalfExtents;
        LocalRotation = localRotation;
    }

    public override BoundingSphere LocalBoundary =>
        new(LocalCenter, LocalHalfExtents.Length());

    public override int PrimitiveCount => 1;

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        var p = Pose(in mine);
        return other.Shape.TestHit(in other.Pose, in p, tester);
    }

    public override bool TestHit(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester)
    {
        var p = Pose(in mine);
        return tester.TestHit(in p, in otherPrim);
    }

    public override bool TryGetContact(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester, out HitContact3D contact)
    {
        var p = Pose(in mine);
        if (other.Shape.TryGetContact(in other.Pose, in p, tester, out contact))
        {
            contact = contact.Flipped();
            return true;
        }
        return false;
    }

    public override bool TryGetContact(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester, out HitContact3D contact)
    {
        var p = Pose(in mine);
        return tester.TryGetContact(in p, in otherPrim, out contact);
    }

    public override void Visit(in Pose3D mine, HitPrimitiveAction3D action) =>
        action(Pose(in mine));

    private HitPrimitive3D Pose(in Pose3D pose) =>
        HitPrimitive3D.Box(
            pose.Transform(LocalCenter),
            LocalHalfExtents * pose.Scale,
            pose.Rotation * LocalRotation);

    public override HitShape3D Translate(Vector3 offset) =>
        new BoxHitShape3D(LocalCenter + offset, LocalHalfExtents, LocalRotation);
}

/// <summary>
/// A <see cref="HitShape3D"/> that is a two-sided oriented rectangle
/// ("wall"). The rectangle is the local XY plane spanned by
/// <see cref="LocalHalfExtents"/>; <see cref="LocalRotation"/>'s local
/// Z axis is the face normal.
/// </summary>
public sealed class WallHitShape3D : HitShape3D
{
    public Vector3 LocalCenter { get; }
    public Vector2 LocalHalfExtents { get; }
    public Quaternion LocalRotation { get; }

    /// <summary>
    /// When true, only register a hit / contact when the other shape's
    /// bounding-sphere centre lies on the +Z (face-normal) side of the
    /// rectangle's supporting plane. Models one-way platforms and
    /// jump-through floors.
    /// </summary>
    public bool OneSided { get; }

    public WallHitShape3D(Vector3 localCenter, Vector2 localHalfExtents)
        : this(localCenter, localHalfExtents, Quaternion.Identity, false) { }

    public WallHitShape3D(Vector3 localCenter, Vector2 localHalfExtents, Quaternion localRotation)
        : this(localCenter, localHalfExtents, localRotation, false) { }

    public WallHitShape3D(Vector3 localCenter, Vector2 localHalfExtents, Quaternion localRotation, bool oneSided)
    {
        LocalCenter = localCenter;
        LocalHalfExtents = localHalfExtents;
        LocalRotation = localRotation;
        OneSided = oneSided;
    }

    public override BoundingSphere LocalBoundary =>
        new(LocalCenter, LocalHalfExtents.Length());

    public override int PrimitiveCount => 1;

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        var p = Pose(in mine);
        if (OneSided && !IsOnOutwardSide(other.BoundingSphere.Center, in p))
            return false;
        return other.Shape.TestHit(in other.Pose, in p, tester);
    }

    public override bool TestHit(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester)
    {
        var p = Pose(in mine);
        if (OneSided && !IsOnOutwardSide(otherPrim.P0, in p))
            return false;
        return tester.TestHit(in p, in otherPrim);
    }

    public override bool TryGetContact(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester, out HitContact3D contact)
    {
        var p = Pose(in mine);
        if (OneSided && !IsOnOutwardSide(other.BoundingSphere.Center, in p))
        {
            contact = default;
            return false;
        }
        if (other.Shape.TryGetContact(in other.Pose, in p, tester, out contact))
        {
            contact = contact.Flipped();
            return true;
        }
        return false;
    }

    public override bool TryGetContact(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester, out HitContact3D contact)
    {
        var p = Pose(in mine);
        if (OneSided && !IsOnOutwardSide(otherPrim.P0, in p))
        {
            contact = default;
            return false;
        }
        return tester.TryGetContact(in p, in otherPrim, out contact);
    }

    public override void Visit(in Pose3D mine, HitPrimitiveAction3D action) =>
        action(Pose(in mine));

    private HitPrimitive3D Pose(in Pose3D pose) =>
        HitPrimitive3D.Wall(
            pose.Transform(LocalCenter),
            LocalHalfExtents * pose.Scale,
            pose.Rotation * LocalRotation);

    // Outward = local +Z (the face normal) rotated by the wall's world
    // quaternion (primitive.Q). Center → world via primitive.P0.
    private static bool IsOnOutwardSide(Vector3 point, in HitPrimitive3D wall)
    {
        var normal = Vector3.Transform(Vector3.UnitZ, wall.Q);
        return Vector3.Dot(point - wall.P0, normal) >= 0f;
    }

    public override HitShape3D Translate(Vector3 offset) =>
        new WallHitShape3D(LocalCenter + offset, LocalHalfExtents, LocalRotation, OneSided);
}
