using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// A barrier based on a mesh. Treats the mesh as immutable from a
/// collision standpoint: the position / face-normal extraction runs
/// once per <see cref="Mesh"/> instance and is shared across every
/// barrier wrapping that mesh.
/// </summary>
public class MeshBarrier3D<TVertex> : Barrier3D
    where TVertex : unmanaged, IPositionVertex3D
{
    private readonly Mesh<TVertex> _mesh;
    private readonly MeshHitShape3D _hitShape;

    /// <summary>
    /// Wraps <paramref name="mesh"/> as a barrier. The mesh's topology
    /// must be <see cref="Topology.TriangleList"/>; vertex positions
    /// are read in whatever space they're authored in, so pre-transform
    /// to world space if the barrier won't move. Later updates to the
    /// mesh's vertex data are not picked up by collision.
    /// </summary>
    public MeshBarrier3D(Mesh<TVertex> mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        _mesh = mesh;
        _hitShape = MeshHitShape3D.For(mesh);
    }

    /// <summary>
    /// The mesh used for both collision and (optionally) drawing.
    /// Override <see cref="Barrier3D.Draw"/> to render it.
    /// </summary>
    public Mesh<TVertex> Mesh => _mesh;

    /// <summary>Number of triangles in the mesh.</summary>
    public int TriangleCount => _hitShape.TriangleCount;

    /// <summary>Broad-phase sphere enclosing every vertex.</summary>
    public BoundingSphere Bounds => _hitShape.Bounds;

    /// <summary>
    /// Per-triangle outward face normals, derived from each triangle's
    /// winding (<c>Cross(v1-v0, v2-v0)</c> normalised). Degenerate
    /// (zero-area) triangles yield <see cref="Vector3.Zero"/>.
    /// </summary>
    public ReadOnlySpan<Vector3> FaceNormals => _hitShape.FaceNormals;

    /// <inheritdoc/>
    public override PosedHitShape3D HitShape =>
        new(_hitShape, Pose3D.Identity);

    /// <summary>True when <paramref name="sphere"/> overlaps any of this mesh's triangles.</summary>
    public bool Intersects(BoundingSphere sphere) => _hitShape.Intersects(sphere);

    /// <summary>
    /// Closed-form sphere-vs-mesh contact: picks the nearest triangle
    /// and uses its outward face normal. <paramref name="normal"/>
    /// points from the surface toward the sphere.
    /// </summary>
    public bool TryGetContact(BoundingSphere sphere, out Vector3 normal, out float penetration) =>
        _hitShape.TryGetContact(sphere, out normal, out penetration);
}
