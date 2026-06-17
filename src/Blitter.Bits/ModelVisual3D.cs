namespace Blitter.Bits;

/// <summary>
/// A <see cref="Visual3D"/> backed by a <see cref="Model"/> (one or
/// more (mesh + material) parts). Each part is drawn through the
/// supplied <see cref="Materializer"/> (defaulting to
/// <see cref="StandardMaterializer.Default"/>).
/// </summary>
public sealed class ModelVisual3D : Visual3D
{
    private readonly Materializer? _materializer;
    private readonly HitShape3DCache _hitShapeCache;
    private HitShape3D? _hitShape;
    private BoundingSphere? _boundary;

    /// <summary>The model rendered by this visual.</summary>
    public Model Model { get; }

    public ModelVisual3D(
        Model model,
        HitShape3D? hitShape = null,
        Materializer? materializer = null,
        HitShape3DCache? hitShapeCache = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
        _materializer = materializer;
        _hitShape = hitShape;
        _hitShapeCache = hitShapeCache ?? HitShape3DCache.Default;
    }

    /// <inheritdoc/>
    public override BoundingSphere Boundary =>
        _boundary ??= Model.ComputeBoundingSphere();

    /// <inheritdoc/>
    public override HitShape3D HitShape =>
        _hitShape ??= _hitShapeCache.GetOrCreateHitShape(Model);

    /// <inheritdoc/>
    public override void Draw(Renderer3D renderer, in Pose3D pose, Color tint, TimeSpan elapsed)
    {
        var transform = pose.ToMatrix();
        // No-tint fast path: hand the whole model to the standard
        // non-instanced extension. One call site, no per-part branching.
        if (tint == Color.White)
        {
            renderer.DrawModel(Model, transform, _materializer);
            return;
        }
        // Tinted path: borrow the *Instanced shader's per-instance
        // color slot by submitting a single stack-allocated instance
        // per part. No heap alloc, no Material mutation. Parts whose
        // material has no instanced binding (e.g. PbrMaterial today)
        // fall back to the non-instanced shader without tint.
        Span<TransformAndColorInstance> one = stackalloc TransformAndColorInstance[1];
        one[0] = new TransformAndColorInstance(transform, tint);
        foreach (var part in Model.Parts)
        {
            if (part.Material is LitTextureMaterial
                && part.Mesh.VertexType == typeof(LitTextureVertex3D))
                renderer.DrawMesh<TransformAndColorInstance>(part.Mesh, part.Material, one, _materializer);
            else
                renderer.DrawMesh(part.Mesh, part.Material, transform, _materializer);
        }
    }
}
