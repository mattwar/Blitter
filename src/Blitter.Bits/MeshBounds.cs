using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Helpers to compute bounds for a <see cref="Mesh"/>
/// </summary>
public static class MeshBounds
{
    /// <summary>
    /// Computes the bounding box of the vertices in the <paramref name="mesh"/>.
    /// </summary>
    public static BoundingBox ComputeBoundingBox<TVertex>(this Mesh<TVertex> mesh)
        where TVertex : unmanaged, IPositionVertex3D
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return BoundingBox.FromVertices(mesh.Vertices);
    }

    /// <summary>
    /// Non-generic <see cref="BoundingBox"/> computation for the stock
    /// vertex layouts. Throws <see cref="NotSupportedException"/> for
    /// any other <c>TVertex</c>.
    /// </summary>
    public static BoundingBox ComputeBoundingBox(this Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return mesh switch
        {
            Mesh<Vertex3D> m            => BoundingBox.FromVertices(m.Vertices),
            Mesh<ColorVertex3D> m       => BoundingBox.FromVertices(m.Vertices),
            Mesh<TextureVertex3D> m     => BoundingBox.FromVertices(m.Vertices),
            Mesh<LitVertex3D> m         => BoundingBox.FromVertices(m.Vertices),
            Mesh<LitTextureVertex3D> m  => BoundingBox.FromVertices(m.Vertices),
            _ => throw new NotSupportedException(
                $"No non-generic bounding-box computation for Mesh<{mesh.VertexType.Name}>. " +
                "Use the generic MeshBounds.ComputeBoundingBox<TVertex> overload, " +
                "or supply the boundary explicitly."),
        };
    }

    /// <summary>
    /// Computes a bounding sphere of the vertices in the <paramref name="mesh"/>.
    /// </summary>
    public static BoundingSphere ComputeBoundingSphere<TVertex>(this Mesh<TVertex> mesh)
        where TVertex : unmanaged, IPositionVertex3D
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return BoundingSphere.FromVertices(mesh.Vertices);
    }

    /// <summary>
    /// Non-generic <see cref="BoundingSphere"/> computation for the
    /// stock vertex layouts (<see cref="Vertex3D"/>,
    /// <see cref="ColorVertex3D"/>, <see cref="TextureVertex3D"/>,
    /// <see cref="LitVertex3D"/>, <see cref="LitTextureVertex3D"/>).
    /// Throws <see cref="NotSupportedException"/> for any other
    /// <c>TVertex</c>; use the generic overload in that case or supply
    /// the boundary explicitly.
    /// </summary>
    public static BoundingSphere ComputeBoundingSphere(this Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return mesh switch
        {
            Mesh<Vertex3D> m            => BoundingSphere.FromVertices(m.Vertices),
            Mesh<ColorVertex3D> m       => BoundingSphere.FromVertices(m.Vertices),
            Mesh<TextureVertex3D> m     => BoundingSphere.FromVertices(m.Vertices),
            Mesh<LitVertex3D> m         => BoundingSphere.FromVertices(m.Vertices),
            Mesh<LitTextureVertex3D> m  => BoundingSphere.FromVertices(m.Vertices),
            _ => throw new NotSupportedException(
                $"No non-generic bounding-sphere computation for Mesh<{mesh.VertexType.Name}>. " +
                "Use the generic MeshBounds.ComputeBoundingSphere<TVertex> overload, " +
                "or supply the boundary explicitly."),
        };
    }

    /// <summary>
    /// Computes the center of the bounding box of the <paramref name="mesh"/>.
    /// </summary>
    public static Vector3 ComputeCenter<TVertex>(this Mesh<TVertex> mesh)
        where TVertex : unmanaged, IPositionVertex3D =>
        mesh.ComputeBoundingBox().Center;

    /// <summary>
    /// Returns a nominal set of bounding boxes that cover the surface of the <paramref name="mesh"/>.
    /// </summary>
    public static BoundingBox[] ComputeOccupiedBoxes<TVertex>(
        this Mesh<TVertex> mesh,
        float voxelSize,
        MeshOccupancyMode mode = MeshOccupancyMode.Accurate)
        where TVertex : unmanaged, IPositionVertex3D
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return MeshOccupancy.ComputeForMesh(mesh, voxelSize, mode);
    }
}
