namespace Blitter.Blocks2D;

/// <summary>
/// Optional, author-supplied collision geometry in the entity's own (unposed,
/// unflipped) space. Pure data. When present and not
/// <see cref="HitShape2D.None"/>, <see cref="DefaultColliderShape2D"/> uses it in
/// preference to the visual's hit shape; otherwise the collider falls back to
/// the visual. Add this trait only when a sprite needs a collision boundary
/// that differs from (or exists without) its visual.
/// </summary>
public sealed class CollisionShape2D : Trait
{
    /// <summary>
    /// The image-local collision boundary. <see cref="HitShape2D.None"/> (the
    /// default) means "no override" — the collider uses the visual instead.
    /// </summary>
    public HitShape2D Shape { get; set; } = HitShape2D.None;
}
