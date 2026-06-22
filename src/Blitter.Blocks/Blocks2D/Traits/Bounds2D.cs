namespace Blitter.Blocks2D;

/// <summary>
/// The world-space rectangle that bounds an entity's contents. Owned by a
/// <see cref="PlayField2D"/> and refreshed each frame; behaviors resolve it
/// by walking up to their playfield (see <c>IEntity.TryFindTrait</c>).
/// </summary>
public sealed class Bounds2D : Trait
{
    /// <summary>The bounding rectangle in world units.</summary>
    public Rect Rect { get; set; }
}
