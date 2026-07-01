namespace Blitter.Blocks2D;

/// <summary>
/// How an entity presents itself: a declarative <see cref="ImageSource"/>
/// describing the look, plus runtime tint and flip. Pure, serializable data —
/// <see cref="Source"/> retains the authoring facts (file, tiles, animation
/// states, hit hint) and is materialised into a <see cref="Visual2D"/> on
/// demand via <see cref="ImageSource.GetComposedVisual"/>.
/// </summary>
public sealed class Appearance2D : Trait
{
    /// <summary>
    /// The declarative description of the look: a file or texture, optionally
    /// tiled or animated through named states. Always non-null; materialise it
    /// with <see cref="ImageSource.GetComposedVisual"/>.
    /// </summary>
    public ImageSource Source { get; set; } = new();

    /// <summary>Tint color applied to the visual.</summary>
    public Color Tint { get; set; } = Color.White;

    /// <summary>
    /// Runtime mirror applied to the visual at draw time and to the hit shape
    /// when collisions are evaluated. Composes with any authoring flip on the
    /// visual's current animation frame.
    /// </summary>
    public FlipMode Flipped { get; set; } = FlipMode.None;
}
