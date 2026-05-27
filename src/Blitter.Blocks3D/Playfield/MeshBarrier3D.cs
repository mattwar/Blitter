using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// A barrier based on a mesh.
/// </summary>
public class MeshBarrier3D<TVertex> : Barrier3D
    where TVertex : unmanaged, IPositionVertex3D
{
    private readonly Mesh<TVertex> _mesh;

    // Hot-path caches. Rebuilt when _mesh.Version changes so that
    // collision-time loops touch a tight Vector3[] (12 B/elem) rather
    // than dragging full vertices (often 32-56 B/elem) through cache.
    private Vector3[] _positions = Array.Empty<Vector3>();
    private Vector3[] _faceNormals = Array.Empty<Vector3>();
    private BoundingSphere _bounds;
    private int _cachedVersion = -1;

    /// <summary>
    /// Wraps <paramref name="mesh"/> as a barrier. The mesh's topology
    /// must be <see cref="Topology.TriangleList"/>; vertex positions
    /// are read in whatever space they're authored in, so pre-transform
    /// to world space if the barrier won't move.
    /// </summary>
    public MeshBarrier3D(Mesh<TVertex> mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.Topology != Topology.TriangleList)
            throw new ArgumentException(
                $"MeshBarrier3D requires {nameof(Topology)}.{nameof(Topology.TriangleList)}; got {mesh.Topology}.",
                nameof(mesh));
        _mesh = mesh;
    }

    /// <summary>
    /// The mesh used for both collision and (optionally) drawing.
    /// Override <see cref="Barrier3D.Draw"/> to render it.
    /// </summary>
    public Mesh<TVertex> Mesh => _mesh;

    /// <summary>Number of triangles in the mesh.</summary>
    public int TriangleCount
    {
        get
        {
            EnsureCache();
            return _positions.Length / 3;
        }
    }

    /// <summary>Broad-phase sphere enclosing every vertex.</summary>
    public BoundingSphere Bounds
    {
        get
        {
            EnsureCache();
            return _bounds;
        }
    }

    /// <summary>
    /// Per-triangle outward face normals, derived from each triangle's
    /// winding (<c>Cross(v1-v0, v2-v0)</c> normalised). Degenerate
    /// (zero-area) triangles yield <see cref="Vector3.Zero"/>.
    /// </summary>
    public ReadOnlySpan<Vector3> FaceNormals
    {
        get
        {
            EnsureCache();
            return _faceNormals;
        }
    }

    /// <inheritdoc/>
    public override PosedHitShape3D HitShape
    {
        get
        {
            EnsureCache();
            // The hit shape borrows the live caches; rebuilt whenever
            // the mesh version changes.
            return new PosedHitShape3D(
                new MeshHitShape3D(_positions, _faceNormals, _bounds),
                Pose3D.Identity);
        }
    }

    /// <summary>True when <paramref name="sphere"/> overlaps any of this mesh's triangles.</summary>
    public bool Intersects(BoundingSphere sphere)
    {
        EnsureCache();
        if (sphere.IsEmpty || !_bounds.Intersects(sphere))
            return false;

        var r2 = sphere.Radius * sphere.Radius;
        var c = sphere.Center;
        var verts = _positions;
        for (int i = 0; i < verts.Length; i += 3)
        {
            var closest = Geometry3D.ClosestPointOnTriangle(c, verts[i], verts[i + 1], verts[i + 2]);
            if (Vector3.DistanceSquared(c, closest) <= r2)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Closed-form sphere-vs-mesh contact: picks the nearest triangle
    /// and uses its outward face normal. <paramref name="normal"/>
    /// points from the surface toward the sphere.
    /// </summary>
    public bool TryGetContact(BoundingSphere sphere, out Vector3 normal, out float penetration)
    {
        normal = default;
        penetration = 0f;
        EnsureCache();
        if (sphere.IsEmpty || !_bounds.Intersects(sphere))
            return false;

        var c = sphere.Center;
        var verts = _positions;
        var faces = _faceNormals;
        var bestDistSq = float.PositiveInfinity;
        var bestNormal = Vector3.UnitY;
        for (int i = 0, t = 0; i < verts.Length; i += 3, t++)
        {
            var closest = Geometry3D.ClosestPointOnTriangle(c, verts[i], verts[i + 1], verts[i + 2]);
            var distSq = Vector3.DistanceSquared(c, closest);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                var face = faces[t];
                if (face.LengthSquared() <= float.Epsilon)
                {
                    var delta = c - closest;
                    var dl = delta.LengthSquared();
                    bestNormal = dl > float.Epsilon ? delta / MathF.Sqrt(dl) : Vector3.UnitY;
                }
                else
                {
                    bestNormal = face;
                }
            }
        }
        if (bestDistSq > sphere.Radius * sphere.Radius)
            return false;
        normal = bestNormal;
        penetration = sphere.Radius - MathF.Sqrt(bestDistSq);
        return true;
    }

    private void EnsureCache()
    {
        if (_cachedVersion == _mesh.Version)
            return;

        var verts = _mesh.Vertices;
        var indices = _mesh.Indices;

        int positionCount;
        if (indices.Length > 0)
        {
            if (indices.Length % 3 != 0)
                throw new InvalidOperationException("Mesh index count is not a multiple of 3.");
            positionCount = indices.Length;
        }
        else
        {
            if (verts.Length % 3 != 0)
                throw new InvalidOperationException("Mesh vertex count is not a multiple of 3.");
            positionCount = verts.Length;
        }

        if (_positions.Length != positionCount)
        {
            _positions = new Vector3[positionCount];
            _faceNormals = new Vector3[positionCount / 3];
        }

        if (indices.Length > 0)
        {
            for (int i = 0; i < indices.Length; i++)
                _positions[i] = verts[(int)indices[i]].Position;
        }
        else
        {
            for (int i = 0; i < verts.Length; i++)
                _positions[i] = verts[i].Position;
        }

        for (int i = 0, t = 0; i < _positions.Length; i += 3, t++)
        {
            var n = Vector3.Cross(_positions[i + 1] - _positions[i], _positions[i + 2] - _positions[i]);
            var lenSq = n.LengthSquared();
            _faceNormals[t] = lenSq > float.Epsilon ? n / MathF.Sqrt(lenSq) : Vector3.Zero;
        }

        _bounds = BoundingSphere.FromPoints(_positions);
        _cachedVersion = _mesh.Version;
    }
}

/// <summary>
/// Placeholder triangle-soup <see cref="HitShape3D"/>. Tests other
/// shapes against its bounding sphere only — full primitive-vs-mesh
/// contact resolution is planned. Used by <see cref="MeshBarrier3D{T}"/>.
/// </summary>
public sealed class MeshHitShape3D : HitShape3D
{
    private readonly Vector3[] _positions;
    private readonly Vector3[] _faceNormals;
    private readonly BoundingSphere _bounds;

    internal MeshHitShape3D(Vector3[] positions, Vector3[] faceNormals, BoundingSphere bounds)
    {
        _positions = positions;
        _faceNormals = faceNormals;
        _bounds = bounds;
    }

    public override BoundingSphere LocalBoundary => _bounds;

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester) =>
        IntersectsSphere(other.BoundingSphere);

    public override bool TestHitWith(in Pose3D mine, ReadOnlySpan<HitPrimitive3D> other, HitTester3D tester)
    {
        // Approximate the other primitive set as their union bounding
        // sphere; full mesh-vs-primitive support is a follow-up.
        var bound = BoundingSphereFromPrimitives(other);
        return !bound.IsEmpty && IntersectsSphere(bound);
    }

    public override bool TryGetContact(in Pose3D mine, in PosedHitShape3D other, ContactHitTester3D tester, out HitContact3D contact)
    {
        var sphere = other.BoundingSphere;
        if (sphere.IsEmpty || !_bounds.Intersects(sphere))
        {
            contact = default;
            return false;
        }
        if (!TryGetSphereContact(sphere, out var normal, out var point, out var penetration))
        {
            contact = default;
            return false;
        }
        // Normal convention: b → a where this shape is `a`. The
        // sphere-vs-mesh math returns the surface normal pointing away
        // from the mesh (i.e. toward the sphere), which is the same
        // direction we need.
        contact = new HitContact3D(normal, point, penetration);
        return true;
    }

    public override bool TryGetContactWith(in Pose3D mine, ReadOnlySpan<HitPrimitive3D> other, ContactHitTester3D tester, out HitContact3D contact)
    {
        var sphere = BoundingSphereFromPrimitives(other);
        if (sphere.IsEmpty || !_bounds.Intersects(sphere))
        {
            contact = default;
            return false;
        }
        if (!TryGetSphereContact(sphere, out var normal, out var point, out var penetration))
        {
            contact = default;
            return false;
        }
        // `other` is the `a` side; this shape is the `b` side. The
        // shape-level convention puts the normal pointing from `b`
        // toward `a`, which matches the surface-normal direction.
        contact = new HitContact3D(normal, point, penetration);
        return true;
    }

    public override void Visit(in Pose3D mine, HitShapeVisitor3D visitor)
    {
        // Triangle soup has no closed-form primitive representation —
        // visitor sees an empty span.
    }

    public override HitShape3D Translate(Vector3 offset) =>
        throw new NotSupportedException(
            $"{nameof(MeshHitShape3D)} caches its mesh data and cannot be translated; translate the source mesh instead.");

    private bool IntersectsSphere(BoundingSphere sphere)
    {
        if (sphere.IsEmpty || !_bounds.Intersects(sphere))
            return false;
        var r2 = sphere.Radius * sphere.Radius;
        var c = sphere.Center;
        var verts = _positions;
        for (int i = 0; i < verts.Length; i += 3)
        {
            var closest = Geometry3D.ClosestPointOnTriangle(c, verts[i], verts[i + 1], verts[i + 2]);
            if (Vector3.DistanceSquared(c, closest) <= r2)
                return true;
        }
        return false;
    }

    private bool TryGetSphereContact(BoundingSphere sphere, out Vector3 normal, out Vector3 point, out float penetration)
    {
        var c = sphere.Center;
        var verts = _positions;
        var faces = _faceNormals;
        var bestDistSq = float.PositiveInfinity;
        var bestNormal = Vector3.UnitY;
        var bestPoint = c;
        for (int i = 0, t = 0; i < verts.Length; i += 3, t++)
        {
            var closest = Geometry3D.ClosestPointOnTriangle(c, verts[i], verts[i + 1], verts[i + 2]);
            var distSq = Vector3.DistanceSquared(c, closest);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestPoint = closest;
                var face = faces[t];
                if (face.LengthSquared() <= float.Epsilon)
                {
                    var delta = c - closest;
                    var dl = delta.LengthSquared();
                    bestNormal = dl > float.Epsilon ? delta / MathF.Sqrt(dl) : Vector3.UnitY;
                }
                else
                {
                    bestNormal = face;
                }
            }
        }
        if (bestDistSq > sphere.Radius * sphere.Radius)
        {
            normal = default;
            point = default;
            penetration = 0f;
            return false;
        }
        normal = bestNormal;
        point = bestPoint;
        penetration = sphere.Radius - MathF.Sqrt(bestDistSq);
        return true;
    }

    private static BoundingSphere BoundingSphereFromPrimitives(ReadOnlySpan<HitPrimitive3D> primitives)
    {
        // All current HitShape3D concretes emit exactly one primitive,
        // so the first entry's broad-phase sphere is the full bound.
        // Multi-primitive shapes will need a proper aggregator here.
        if (primitives.Length == 0)
            return BoundingSphere.Empty;
        return PrimitiveBoundingSphere(in primitives[0]);
    }

    private static BoundingSphere PrimitiveBoundingSphere(in HitPrimitive3D p)
    {
        switch (p.Kind)
        {
            case HitKind3D.Sphere:
                return new BoundingSphere(p.P0, p.R);
            case HitKind3D.Capsule:
            case HitKind3D.Cylinder:
            {
                var mid = (p.P0 + p.P1) * 0.5f;
                var half = (p.P1 - p.P0).Length() * 0.5f;
                return new BoundingSphere(mid, half + p.R);
            }
            case HitKind3D.Box:
                return new BoundingSphere(p.P0, p.P1.Length());
            case HitKind3D.Wall:
                return new BoundingSphere(p.P0, new Vector2(p.P1.X, p.P1.Y).Length());
            default:
                return BoundingSphere.Empty;
        }
    }
}
