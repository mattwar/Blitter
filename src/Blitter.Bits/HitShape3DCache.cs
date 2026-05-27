using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Blitter.Bits;

/// <summary>
/// Caches a <see cref="HitShape3D"/> per <see cref="Mesh"/> (and per
/// <see cref="Model"/>), sharing computed shapes across all visuals
/// that use the same cache instance. Subclass and override
/// <see cref="ComputeHitShape(Mesh)"/> or
/// <see cref="ComputeHitShape(Model)"/> to change how shapes are
/// derived; pass the subclass to a visual's constructor to use it.
/// The 3D analog of <see cref="HitShapeCache"/>.
/// </summary>
public class HitShape3DCache
{
    /// <summary>Process-wide default cache used by visuals when none is supplied.</summary>
    public static HitShape3DCache Default { get; } = new();

    private readonly ConditionalWeakTable<Mesh, HitShape3D> _meshShapes = new();
    private readonly ConditionalWeakTable<Model, HitShape3D> _modelShapes = new();

    // Cached delegates so GetValue doesn't allocate per lookup.
    private readonly ConditionalWeakTable<Mesh, HitShape3D>.CreateValueCallback _meshCallback;
    private readonly ConditionalWeakTable<Model, HitShape3D>.CreateValueCallback _modelCallback;

    public HitShape3DCache()
    {
        _meshCallback = ComputeHitShape;
        _modelCallback = ComputeHitShape;
    }

    /// <summary>Returns the cached fit for <paramref name="mesh"/>, computing it on first use.</summary>
    public HitShape3D GetOrCreateHitShape(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return _meshShapes.GetValue(mesh, _meshCallback);
    }

    /// <summary>Returns the cached fit for <paramref name="model"/>, computing it on first use.</summary>
    public HitShape3D GetOrCreateHitShape(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return _modelShapes.GetValue(model, _modelCallback);
    }

    /// <summary>
    /// Computes a hit shape from <paramref name="mesh"/>. Default
    /// behavior delegates to <see cref="MeshFit3D.ComputeAutoHitShape3D"/>.
    /// </summary>
    protected virtual HitShape3D ComputeHitShape(Mesh mesh) =>
        mesh.ComputeAutoHitShape3D();

    /// <summary>
    /// Computes a hit shape from <paramref name="model"/>. Default
    /// behavior fits each part's mesh independently (via the mesh
    /// cache) and wraps the results in a
    /// <see cref="CompositeHitShape3D"/>.
    /// </summary>
    protected virtual HitShape3D ComputeHitShape(Model model)
    {
        var parts = model.Parts;
        if (parts.Length == 0) return HitShape3D.None;
        if (parts.Length == 1) return GetOrCreateHitShape(parts[0].Mesh);

        var builder = ImmutableArray.CreateBuilder<HitShape3D>(parts.Length);
        for (int i = 0; i < parts.Length; i++)
            builder.Add(GetOrCreateHitShape(parts[i].Mesh));
        return new CompositeHitShape3D(builder.ToImmutable());
    }
}
