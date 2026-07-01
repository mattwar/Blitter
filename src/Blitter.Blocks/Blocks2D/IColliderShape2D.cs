namespace Blitter.Blocks2D;

/// <summary>
/// Capability for a <see cref="Behavior"/> that supplies an entity's
/// world-space collision shape. The collision pass (<see cref="Collider2D"/>)
/// and an entity's <c>HitShape</c> accessor resolve the shape through this
/// interface rather than a concrete behavior, so an entity may install an
/// alternate provider in place of the default <see cref="DefaultColliderShape2D"/>.
/// </summary>
public interface IColliderShape2D
{
    /// <summary>The entity's collision shape in world space.</summary>
    PosedHitShape2D GetShape();
}
