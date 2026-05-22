namespace Blitter.Bits;

/// <summary>
/// Something drawable in 2D with a bounding circle and a <see cref="HitShape2D"/>.
/// </summary>
public abstract class Visual2D
{
    /// <summary>
    /// Name of the implicit single state for visuals that have no
    /// animation or sequence concept.
    /// </summary>
    public const string DefaultState = "default";

    private static readonly IReadOnlyList<string> _defaultStates = [DefaultState];

    /// <summary>
    /// Currently selected animation, pose, or variant name. Setting
    /// it switches which frames the visual draws and which
    /// <see cref="Boundary"/> / <see cref="HitShape"/> it reports.
    /// Single-state visuals keep this at <see cref="DefaultState"/>.
    /// </summary>
    public virtual string State { get; set; } = DefaultState;

    /// <summary>
    /// All state names this visual can switch between, in declaration order.
    /// Single-state visuals expose just <see cref="DefaultState"/>.
    /// </summary>
    public virtual IReadOnlyList<string> States => _defaultStates;

    /// <summary>
    /// Bounding circle in visual-local coordinates (origin at the visual's center, unscaled). 
    /// The caller applies its own position/scale when computing world-space collision.
    /// </summary>
    public abstract BoundingCircle Boundary { get; }

    /// <summary>
    /// Visual-local collision shape. 
    /// Defaults to a single circle derived from <see cref="Boundary"/>; 
    /// assign a hand-rolled shape (e.g. a capsule along the visible body) for tighter geometry.
    /// </summary>
    public HitShape2D HitShape
    {
        get => _hitShape ??= DeriveHitShape();
        set => _hitShape = value;
    }

    private HitShape2D? _hitShape;

    /// <summary>
    /// Produces the default <see cref="HitShape2D"/> when none has been explicitly set. 
    /// The base implementation returns a circle from <see cref="Boundary"/>; 
    /// subclasses with pixel access can fit a tighter shape.
    /// </summary>
    protected virtual HitShape2D DeriveHitShape() =>
        new CircleHitShape2D(Boundary.Center, Boundary.Radius);

    /// <summary>
    /// Drops the cached <see cref="HitShape"/> so the next access re-derives it.
    /// Use after a change that would affect collision geometry. A shape that
    /// was explicitly assigned to <see cref="HitShape"/> is also cleared.
    /// </summary>
    protected void InvalidateHitShape() => _hitShape = null;

    /// <summary>
    /// Draw this visual at the given world <paramref name="pose"/>, with the specified <paramref name="tint"/>. 
    /// Pass <see cref="Color.White"/> for untinted output. 
    /// Pass <paramref name="elapsed"/> as the host's age — animated visuals use it to pick the current frame;
    /// static visuals ignore it.
    /// </summary>
    public abstract void Draw(Renderer2D renderer, in Pose2D pose, Color tint, TimeSpan elapsed);

    /// <summary>
    /// Implicit wrap of a <see cref="Texture2D"/> in a <see cref="TextureVisual2D"/> so callers can assign a texture
    /// directly to a visual-typed property.
    /// </summary>
    public static implicit operator Visual2D(Texture2D texture) =>
        new TextureVisual2D(texture);
}
