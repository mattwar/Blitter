namespace Blitter.Blocks2D;

/// <summary>
/// Optional world-space rectangle that bounds an entity's contents. Behaviors
/// resolve it by walking up the entity tree (see <c>IEntity.TryFindTrait</c>).
/// </summary>
public sealed class Bounds2D : Trait
{
    /// <summary>The bounding rectangle in world units.</summary>
    public Rect? Rect { get; set; }
}
