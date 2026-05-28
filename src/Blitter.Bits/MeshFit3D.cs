using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Picks a "best" <see cref="HitShape3D"/> for a <see cref="Mesh"/> by
/// fitting box / sphere / capsule / cylinder candidates over its
/// vertices and returning the smallest-volume one that still encloses
/// every vertex. The 3D analog of
/// <see cref="ImageBounds.ComputeOpaqueHitShape2D"/>.
/// </summary>
public static class MeshFit3D
{
    // Capsule/cylinder candidates only make sense for noticeably
    // elongated AABBs; otherwise they degenerate toward a sphere. Match
    // the 2D elongation threshold (longest > 1.4 * second-longest).
    private const float ElongationThreshold = 1.4f;

    /// <summary>
    /// Returns the tightest of <see cref="BoxHitShape3D"/>,
    /// <see cref="SphereHitShape3D"/>, <see cref="CapsuleHitShape3D"/>,
    /// or <see cref="CylinderHitShape3D"/> that encloses every vertex
    /// of the mesh. Capsule and cylinder candidates are only considered
    /// when the AABB is sufficiently elongated along one axis; both are
    /// always axis-aligned with the longest AABB extent (no OBB fit).
    /// </summary>
    public static HitShape3D ComputeAutoHitShape3D(this Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.VertexCount == 0) return HitShape3D.None;

        var box = mesh.ComputeBoundingBox();
        if (box.IsEmpty) return HitShape3D.None;

        var extents = box.Size; // full extents along each axis
        var center = box.Center;
        var halfExtents = box.Extents;

        HitShape3D best = new BoxHitShape3D(center, halfExtents);
        float bestVol = extents.X * extents.Y * extents.Z;

        // Sphere candidate (Ritter).
        var sphere = mesh.ComputeBoundingSphere();
        if (!sphere.IsEmpty)
        {
            float r = sphere.Radius;
            float sphereVol = (4f / 3f) * MathF.PI * r * r * r;
            if (sphereVol < bestVol)
            {
                best = new SphereHitShape3D(sphere.Center, r);
                bestVol = sphereVol;
            }
        }

        // Long-axis candidates: only meaningful when the AABB has a
        // clear long direction (e.g., capsule, cylinder, missile).
        PickLongAxis(extents, out Vector3 axis, out float eLong, out float eMid);
        if (eLong >= eMid * ElongationThreshold && eLong > 0f)
        {
            float halfSeg = (eLong - eMid) * 0.5f;
            MeasureRadial(mesh, center, axis, halfSeg, out float capR, out float cylR);

            // Capsule along the long AABB axis.
            float capLen = halfSeg * 2f;
            float capVol = MathF.PI * capR * capR * capLen
                         + (4f / 3f) * MathF.PI * capR * capR * capR;
            if (capVol < bestVol)
            {
                best = new CapsuleHitShape3D(center - axis * halfSeg, center + axis * halfSeg, capR);
                bestVol = capVol;
            }

            // Flat-capped cylinder along the long AABB axis.
            float cylHalf = eLong * 0.5f;
            float cylVol = MathF.PI * cylR * cylR * eLong;
            if (cylVol < bestVol)
            {
                best = new CylinderHitShape3D(center - axis * cylHalf, center + axis * cylHalf, cylR);
                bestVol = cylVol;
            }
        }

        return best;
    }

    private static void PickLongAxis(Vector3 extents, out Vector3 axis, out float eLong, out float eMid)
    {
        // The "second-longest" extent is the right comparison: it's
        // what the candidate's natural radius matches, so it tells us
        // whether the AABB is elongated at all.
        if (extents.X >= extents.Y && extents.X >= extents.Z)
        {
            axis = Vector3.UnitX;
            eLong = extents.X;
            eMid = MathF.Max(extents.Y, extents.Z);
        }
        else if (extents.Y >= extents.Z)
        {
            axis = Vector3.UnitY;
            eLong = extents.Y;
            eMid = MathF.Max(extents.X, extents.Z);
        }
        else
        {
            axis = Vector3.UnitZ;
            eLong = extents.Z;
            eMid = MathF.Max(extents.X, extents.Y);
        }
    }

    // Computes, in a single pass, the minimum radius that encloses
    // every vertex around (a) the line segment of length 2*halfSeg
    // centered at `center` along `axis` (the capsule), and (b) the
    // full infinite line through `center` along `axis` (the cylinder).
    private static void MeasureRadial(
        Mesh mesh, Vector3 center, Vector3 axis, float halfSeg,
        out float capsuleRadius, out float cylinderRadius)
    {
        switch (mesh)
        {
            case Mesh<Vertex3D> m:           Measure(m.Vertices, center, axis, halfSeg, out capsuleRadius, out cylinderRadius); return;
            case Mesh<ColorVertex3D> m:      Measure(m.Vertices, center, axis, halfSeg, out capsuleRadius, out cylinderRadius); return;
            case Mesh<TextureVertex3D> m:    Measure(m.Vertices, center, axis, halfSeg, out capsuleRadius, out cylinderRadius); return;
            case Mesh<LitVertex3D> m:        Measure(m.Vertices, center, axis, halfSeg, out capsuleRadius, out cylinderRadius); return;
            case Mesh<LitTextureVertex3D> m: Measure(m.Vertices, center, axis, halfSeg, out capsuleRadius, out cylinderRadius); return;
            default:
                throw new NotSupportedException(
                    $"No auto-fit radial pass for Mesh<{mesh.VertexType.Name}>. " +
                    "Use one of the stock vertex layouts, or supply an explicit hit shape.");
        }
    }

    private static void Measure<TVertex>(
        ReadOnlySpan<TVertex> verts, Vector3 center, Vector3 axis, float halfSeg,
        out float capsuleRadius, out float cylinderRadius)
        where TVertex : unmanaged, IPositionVertex3D
    {
        float maxSegSq = 0f;
        float maxAxisSq = 0f;
        for (int i = 0; i < verts.Length; i++)
        {
            var p = verts[i].Position;
            var d = p - center;
            float along = Vector3.Dot(d, axis);
            var perp = d - along * axis;
            float perpSq = perp.LengthSquared();
            if (perpSq > maxAxisSq) maxAxisSq = perpSq;

            float t = Math.Clamp(along, -halfSeg, halfSeg);
            var closest = center + t * axis;
            var sd = p - closest;
            float segSq = sd.LengthSquared();
            if (segSq > maxSegSq) maxSegSq = segSq;
        }
        capsuleRadius = MathF.Sqrt(maxSegSq);
        cylinderRadius = MathF.Sqrt(maxAxisSq);
    }
}
