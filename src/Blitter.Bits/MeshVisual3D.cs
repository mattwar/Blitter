using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// A <see cref="Visual3D"/> backed by a single <see cref="Mesh"/> and
/// <see cref="Material"/>. Drawn through the renderer's material-aware
/// extensions, picking a shader via the supplied
/// <see cref="Materializer"/> (defaulting to
/// <see cref="StandardMaterializer.Default"/>).
/// </summary>
public sealed class MeshVisual3D : Visual3D
{
    private readonly Materializer? _materializer;
    private readonly HitShape3DCache _hitShapeCache;
    private HitShape3D? _hitShape;
    private BoundingSphere? _boundary;

    /// <summary>The mesh rendered by this visual.</summary>
    public Mesh Mesh { get; }

    /// <summary>Material used to shade <see cref="Mesh"/>. Mutable so callers can swap surface looks at runtime.</summary>
    public Material Material { get; set; }

    public MeshVisual3D(
        Mesh mesh,
        Material material,
        HitShape3D? hitShape = null,
        Materializer? materializer = null,
        HitShape3DCache? hitShapeCache = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(material);
        Mesh = mesh;
        Material = material;
        _materializer = materializer;
        _hitShape = hitShape;
        _hitShapeCache = hitShapeCache ?? HitShape3DCache.Default;
    }

    /// <inheritdoc/>
    public override BoundingSphere Boundary =>
        _boundary ??= Mesh.ComputeBoundingSphere();

    /// <inheritdoc/>
    public override HitShape3D HitShape =>
        _hitShape ??= _hitShapeCache.GetOrCreateHitShape(Mesh);

    /// <inheritdoc/>
    public override void Draw(Renderer3D renderer, in Pose3D pose, Color tint, TimeSpan elapsed)
    {
        var transform = pose.ToMatrix();
        // No-tint fast path: straight non-instanced draw, no overhead.
        // Tinted path: borrow the existing *Instanced shader (which
        // already multiplies a per-instance color into the fragment
        // result) by submitting a single stack-allocated instance. No
        // heap alloc, no Material mutation, no shader changes needed.
        if (tint == Color.White || Material is not LitTextureMaterial)
        {
            renderer.DrawMesh(Mesh, Material, transform, _materializer);
            return;
        }
        Span<TransformAndColorInstance> one = stackalloc TransformAndColorInstance[1];
        one[0] = new TransformAndColorInstance(transform, tint);
        renderer.DrawMesh<TransformAndColorInstance>(Mesh, Material, one, _materializer);
    }

    // ---- Primitive factories ----------------------------------------
    //
    // Each builds the matching mesh from `Meshes.*` and wires up the
    // tightest `HitShape3D` we have for that shape. Mesh vertex colors
    // already carry the tint, so the material is a plain default
    // `LitTextureMaterial` (white, no texture) for the no-texture
    // factories.

    /// <summary>Solid box centered at the origin.</summary>
    public static MeshVisual3D Cube(Color color, Vector3? size = null)
    {
        var s = size ?? Vector3.One;
        return new MeshVisual3D(
            Meshes.Cube(color, s),
            LitTextureMaterial.Default,
            new BoxHitShape3D(Vector3.Zero, s * 0.5f));
    }

    /// <summary>Smooth (UV) sphere centered at the origin.</summary>
    public static MeshVisual3D Sphere(
        Color color, float radius = 0.5f, int latitudeSegments = 16, int longitudeSegments = 32)
    {
        return new MeshVisual3D(
            Meshes.Sphere(color, radius, latitudeSegments, longitudeSegments),
            LitTextureMaterial.Default,
            new SphereHitShape3D(Vector3.Zero, radius));
    }

    /// <summary>
    /// Capsule along the +Y axis: cylindrical body of length
    /// <paramref name="height"/> with hemispherical caps of
    /// <paramref name="radius"/>. Total Y extent is
    /// <c>height + 2 * radius</c>.
    /// </summary>
    public static MeshVisual3D Capsule(
        Color color, float radius = 0.25f, float height = 0.5f,
        int segments = 24, int hemisphereRings = 8)
    {
        var h = height * 0.5f;
        return new MeshVisual3D(
            Meshes.Capsule(color, radius, height, segments, hemisphereRings),
            LitTextureMaterial.Default,
            new CapsuleHitShape3D(new Vector3(0, -h, 0), new Vector3(0, h, 0), radius));
    }

    /// <summary>Cylinder along the +Y axis with flat caps.</summary>
    public static MeshVisual3D Cylinder(
        Color color, float radius = 0.5f, float height = 1f, int segments = 24, bool capped = true)
    {
        var h = height * 0.5f;
        return new MeshVisual3D(
            Meshes.Cylinder(color, radius, height, segments, capped),
            LitTextureMaterial.Default,
            new CylinderHitShape3D(new Vector3(0, -h, 0), new Vector3(0, h, 0), radius));
    }

    /// <summary>
    /// Flat plane on the XZ axis (normal = +Y). Bounded by
    /// <paramref name="size"/> on its X and Z extents.
    /// </summary>
    public static MeshVisual3D Plane(Color color, Vector2? size = null, int subdivisions = 1)
    {
        var sz = size ?? Vector2.One;
        // WallHitShape3D's local rectangle lies in the local XY plane
        // (normal = local +Z). Rotate -90° about X so the wall's local
        // +Z lands on world +Y, matching the mesh's normal.
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI * 0.5f);
        return new MeshVisual3D(
            Meshes.Plane(color, sz, subdivisions),
            LitTextureMaterial.Default,
            new WallHitShape3D(Vector3.Zero, sz * 0.5f, rotation));
    }

    /// <summary>Cone with apex at +Y. Box fallback for collision.</summary>
    public static MeshVisual3D Cone(
        Color color, float radius = 0.5f, float height = 1f, int segments = 24, bool capped = true) =>
        new(Meshes.Cone(color, radius, height, segments, capped), LitTextureMaterial.Default);

    /// <summary>Torus on the XZ plane. Box fallback for collision.</summary>
    public static MeshVisual3D Torus(
        Color color, float majorRadius = 0.5f, float minorRadius = 0.15f,
        int majorSegments = 32, int minorSegments = 16) =>
        new(Meshes.Torus(color, majorRadius, minorRadius, majorSegments, minorSegments),
            LitTextureMaterial.Default);

    /// <summary>Geodesic (icosphere) sphere centered at the origin.</summary>
    public static MeshVisual3D Icosphere(Color color, float radius = 0.5f, int subdivisions = 2) =>
        new(Meshes.Icosphere(color, radius, subdivisions),
            LitTextureMaterial.Default,
            new SphereHitShape3D(Vector3.Zero, radius));

    /// <summary>Regular tetrahedron. Sphere fallback for collision.</summary>
    public static MeshVisual3D Tetrahedron(Color color, float radius = 0.5f) =>
        new(Meshes.Tetrahedron(color, radius),
            LitTextureMaterial.Default,
            new SphereHitShape3D(Vector3.Zero, radius));

    /// <summary>Regular octahedron. Sphere fallback for collision.</summary>
    public static MeshVisual3D Octahedron(Color color, float radius = 0.5f) =>
        new(Meshes.Octahedron(color, radius),
            LitTextureMaterial.Default,
            new SphereHitShape3D(Vector3.Zero, radius));

    /// <summary>Regular icosahedron. Sphere fallback for collision.</summary>
    public static MeshVisual3D Icosahedron(Color color, float radius = 0.5f) =>
        new(Meshes.Icosahedron(color, radius),
            LitTextureMaterial.Default,
            new SphereHitShape3D(Vector3.Zero, radius));
}
