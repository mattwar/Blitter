namespace Blitter.Bits;

/// <summary>
/// Something drawable in 2D with a bounding circle and a <see cref="HitShape2D"/>.
/// </summary>
public abstract class Visual2D
{
    /// <summary>
    /// Name of the implicit single state for visuals that have no animation or sequence concept.
    /// </summary>
    public const string DefaultState = "default";

    private static readonly IReadOnlyList<string> _defaultStates = [DefaultState];

    /// <summary>
    /// Currently selected visual state.
    /// </summary>
    public virtual string State { get; set; } = DefaultState;

    /// <summary>
    /// All the possible visual states.
    /// </summary>
    public virtual IReadOnlyList<string> States => _defaultStates;

    /// <summary>
    /// The unposed <see cref="BoundingCircle"/> for the visual.
    /// The user applies a <see cref="Pose2D"/> to transform the boundary to world space when testing for collision.
    /// </summary>
    public abstract BoundingCircle Boundary { get; }

    /// <summary>
    /// An unposed <see cref="HitShape2D"/> for the current state of the visual.
    /// The user applies a <see cref="Pose2D"/> to transform the shape to world space when testing for collision.
    /// </summary>
    public abstract HitShape2D HitShape { get; }

    /// <summary>
    /// A time-varying unposed <see cref="HitShape2D"/> for the current state of the visual.
    /// The users applies a <see cref="Pose2D"/> to transform the shape to world space when testing for collision.
    /// </summary>
    public virtual HitShape2D GetHitShapeAt(TimeSpan elapsed) => HitShape;

    /// <summary>
    /// Draws this visual with the specified pose, color tint, and time.
    /// <paramref name="flip"/> is a runtime mirror applied on top of
    /// any per-frame authoring flip (composed via XOR).
    /// </summary>
    public abstract void Draw(Renderer2D renderer, in Pose2D pose, Color tint, TimeSpan elapsed, FlipMode flip = FlipMode.None);

    /// <summary>
    /// Auto convert <see cref="Texture2D"/> to a <see cref="TextureVisual2D"/> so users can assign a texture
    /// directly to a visual-typed property.
    /// </summary>
    public static implicit operator Visual2D(Texture2D texture) =>
        new TextureVisual2D(texture);
}
