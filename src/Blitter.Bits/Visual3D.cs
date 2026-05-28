namespace Blitter.Bits;

/// <summary>
/// Something drawable in 3D with a bounding sphere and a <see cref="HitShape3D"/>.
/// The 3D analog of <see cref="Visual2D"/>.
/// </summary>
public abstract class Visual3D
{
    /// <summary>
    /// Name of the implicit single state for visuals that have no animation or sequence concept.
    /// </summary>
    public const string DefaultState = "default";

    private static readonly IReadOnlyList<string> _defaultStates = [DefaultState];

    /// <summary>Currently selected visual state.</summary>
    public virtual string State { get; set; } = DefaultState;

    /// <summary>All the possible visual states.</summary>
    public virtual IReadOnlyList<string> States => _defaultStates;

    /// <summary>
    /// The unposed <see cref="BoundingSphere"/> for the visual. The user
    /// applies a <see cref="Pose3D"/> to transform the boundary to world
    /// space when testing for collision.
    /// </summary>
    public abstract BoundingSphere Boundary { get; }

    /// <summary>
    /// An unposed <see cref="HitShape3D"/> for the current state of the
    /// visual. The user applies a <see cref="Pose3D"/> to transform the
    /// shape to world space when testing for collision.
    /// </summary>
    public abstract HitShape3D HitShape { get; }

    /// <summary>
    /// A time-varying unposed <see cref="HitShape3D"/> for the current
    /// state of the visual. The user applies a <see cref="Pose3D"/> to
    /// transform the shape to world space when testing for collision.
    /// </summary>
    public virtual HitShape3D GetHitShapeAt(TimeSpan elapsed) => HitShape;

    /// <summary>
    /// Draws this visual with the specified pose, color tint, and time.
    /// </summary>
    public abstract void Draw(Renderer3D renderer, in Pose3D pose, Color tint, TimeSpan elapsed);

    /// <summary>
    /// Auto-converts a <see cref="Mesh"/> to a <see cref="MeshVisual3D"/>
    /// with a default white <see cref="LitTextureMaterial"/>, so users
    /// can assign a mesh directly to a visual-typed property.
    /// </summary>
    public static implicit operator Visual3D(Mesh mesh) =>
        new MeshVisual3D(mesh, LitTextureMaterial.Default);

    /// <summary>
    /// Auto-converts a <see cref="Model"/> to a <see cref="ModelVisual3D"/>.
    /// </summary>
    public static implicit operator Visual3D(Model model) =>
        new ModelVisual3D(model);
}
