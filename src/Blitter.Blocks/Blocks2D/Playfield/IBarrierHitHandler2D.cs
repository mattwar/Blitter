namespace Blitter.Blocks2D;

/// <summary>
/// Capability for a <see cref="Behavior"/> on a <see cref="Barrier2D"/> that
/// responds to a sprite colliding with it. The <see cref="PlayField2D"/> scans
/// a barrier's behaviors for these; the barrier itself exposes no collision
/// API. This mirrors <see cref="IHitHandler2D"/> on the sprite side.
/// </summary>
public interface IBarrierHitHandler2D
{
    /// <summary>
    /// Invoked when <paramref name="sprite"/> collides with
    /// <paramref name="self"/> during the playfield's collision detection.
    /// </summary>
    void OnHitSprite(Barrier2D self, Sprite2D sprite, in UpdateContext context);
}
