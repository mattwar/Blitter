using System.Numerics;
using System.Runtime.CompilerServices;

namespace Blitter.Bits;

/// <summary>
/// Triangle-soup <see cref="HitShape3D"/> backed by a flat array of
/// vertex positions (three per triangle) plus matching outward face
/// normals. One instance is shared per source <see cref="Mesh"/>;
/// use <see cref="For{TVertex}(Mesh{TVertex})"/> to obtain it.
/// </summary>
public sealed class MeshHitShape3D : HitShape3D
{
    // One shape per source mesh. Weak so meshes can still be GC'd
    // once nothing else holds them.
    private static readonly ConditionalWeakTable<Mesh, MeshHitShape3D> _cache = new();

    private readonly Vector3[] _positions;
    private readonly Vector3[] _faceNormals;
    private readonly BoundingSphere _bounds;

    private MeshHitShape3D(Vector3[] positions, Vector3[] faceNormals, BoundingSphere bounds)
    {
        _positions = positions;
        _faceNormals = faceNormals;
        _bounds = bounds;
    }

    /// <summary>
    /// Returns the shared hit shape for <paramref name="mesh"/>, building
    /// it on first request and reusing the same instance for every later
    /// caller against the same mesh. The mesh is treated as immutable:
    /// later vertex updates are not reflected in the returned shape.
    /// </summary>
    public static MeshHitShape3D For<TVertex>(Mesh<TVertex> mesh)
        where TVertex : unmanaged, IPositionVertex3D
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.Topology != Topology.TriangleList)
            throw new ArgumentException(
                $"{nameof(MeshHitShape3D)} requires {nameof(Topology)}.{nameof(Topology.TriangleList)}; got {mesh.Topology}.",
                nameof(mesh));
        // The factory closure captures TVertex; only runs on cache miss.
        return _cache.GetValue(mesh, m => Build((Mesh<TVertex>)m));
    }

    public override BoundingSphere LocalBoundary => _bounds;

    public override int PrimitiveCount => _positions.Length / 3;

    /// <summary>Number of triangles in the mesh.</summary>
    public int TriangleCount => _positions.Length / 3;

    /// <summary>Broad-phase sphere enclosing every vertex.</summary>
    public BoundingSphere Bounds => _bounds;

    /// <summary>Flat triangle-soup vertex positions, three per triangle.</summary>
    public ReadOnlySpan<Vector3> Positions => _positions;

    /// <summary>
    /// Per-triangle outward face normals; degenerate triangles yield
    /// <see cref="Vector3.Zero"/>.
    /// </summary>
    public ReadOnlySpan<Vector3> FaceNormals => _faceNormals;

    /// <summary>True when <paramref name="sphere"/> overlaps any triangle.</summary>
    public bool Intersects(BoundingSphere sphere)
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

    /// <summary>
    /// Closed-form sphere-vs-mesh contact: picks the nearest triangle
    /// and uses its outward face normal. <paramref name="normal"/>
    /// points from the surface toward the sphere.
    /// </summary>
    public bool TryGetContact(BoundingSphere sphere, out Vector3 normal, out float penetration)
    {
        normal = default;
        penetration = 0f;
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

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        var verts = _positions;
        // Per-tri broad-phase prune is sphere-vs-triangle (not the
        // cheapest test) so only do it when the other side has more
        // than one primitive to skip.
        bool prune = other.Shape.PrimitiveCount > 1;
        HitPrimitive3D otherBound = prune ? (HitPrimitive3D)other.BoundingSphere : default;
        for (int i = 0; i < verts.Length; i += 3)
        {
            var tri = TrianglePrimitive(in mine, verts, i);
            if (prune && !tester.TestHit(in tri, in otherBound))
                continue;
            if (other.Shape.TestHit(in other.Pose, in tri, tester))
                return true;
        }
        return false;
    }

    public override bool TestHit(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester)
    {
        var verts = _positions;
        for (int i = 0; i < verts.Length; i += 3)
        {
            var tri = TrianglePrimitive(in mine, verts, i);
            if (tester.TestHit(in tri, in otherPrim))
                return true;
        }
        return false;
    }

    public override bool TryGetContact(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester, out HitContact3D contact)
    {
        var verts = _positions;
        bool found = false;
        HitContact3D best = default;
        bool prune = other.Shape.PrimitiveCount > 1;
        HitPrimitive3D otherBound = prune ? (HitPrimitive3D)other.BoundingSphere : default;
        for (int i = 0; i < verts.Length; i += 3)
        {
            var tri = TrianglePrimitive(in mine, verts, i);
            if (prune && !tester.TestHit(in tri, in otherBound))
                continue;
            // other.TryGetContact(tri) returns "from tri → other"; flip
            // to "from other → me".
            if (other.Shape.TryGetContact(in other.Pose, in tri, tester, out var c))
            {
                c = c.Flipped();
                if (!found || c.Penetration > best.Penetration)
                {
                    best = c;
                    found = true;
                }
            }
        }
        contact = best;
        return found;
    }

    public override bool TryGetContact(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester, out HitContact3D contact)
    {
        var verts = _positions;
        bool found = false;
        HitContact3D best = default;
        for (int i = 0; i < verts.Length; i += 3)
        {
            var tri = TrianglePrimitive(in mine, verts, i);
            // tester.TryGetContact(tri, otherPrim) returns "from otherPrim → tri"
            // = "from external → me". Receiver convention; no flip.
            if (tester.TryGetContact(in tri, in otherPrim, out var c)
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
        var verts = _positions;
        for (int i = 0; i < verts.Length; i += 3)
        {
            var tri = TrianglePrimitive(in mine, verts, i);
            action(in tri);
        }
    }

    private static HitPrimitive3D TrianglePrimitive(in Pose3D pose, Vector3[] verts, int i) =>
        HitPrimitive3D.Triangle(
            pose.Transform(verts[i]),
            pose.Transform(verts[i + 1]),
            pose.Transform(verts[i + 2]));

    private static MeshHitShape3D Build<TVertex>(Mesh<TVertex> mesh)
        where TVertex : unmanaged, IPositionVertex3D
    {
        var verts = mesh.Vertices;
        var indices = mesh.Indices;

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

        var positions = new Vector3[positionCount];
        if (indices.Length > 0)
        {
            for (int i = 0; i < indices.Length; i++)
                positions[i] = verts[(int)indices[i]].Position;
        }
        else
        {
            for (int i = 0; i < verts.Length; i++)
                positions[i] = verts[i].Position;
        }

        var faceNormals = new Vector3[positionCount / 3];
        for (int i = 0, t = 0; i < positions.Length; i += 3, t++)
        {
            var n = Vector3.Cross(positions[i + 1] - positions[i], positions[i + 2] - positions[i]);
            var lenSq = n.LengthSquared();
            faceNormals[t] = lenSq > float.Epsilon ? n / MathF.Sqrt(lenSq) : Vector3.Zero;
        }

        var bounds = BoundingSphere.FromPoints(positions);
        return new MeshHitShape3D(positions, faceNormals, bounds);
    }
}
