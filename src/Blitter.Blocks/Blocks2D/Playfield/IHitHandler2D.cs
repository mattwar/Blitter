namespace Blitter.Blocks2D;

/// <summary>
/// Capability for a <see cref="Behavior"/> that responds to collisions the
/// <see cref="PlayField2D"/> detects. The playfield invokes these on each
/// behavior of a sprite involved in a hit; the sprite itself exposes no
/// collision API. Implement only the method you need — both default to no-ops.
/// </summary>
public interface IHitHandler2D
{
    /// <summary>
    /// Invoked when <paramref name="self"/>'s <see cref="Sprite2D.HitCircle"/>
    /// overlaps another sprite's during the playfield's collision detection.
    /// </summary>
    void OnHitSprite(Sprite2D self, Sprite2D other, in UpdateContext context) { }

    /// <summary>
    /// Invoked when <paramref name="self"/>'s <see cref="Sprite2D.HitCircle"/>
    /// overlaps a <see cref="Barrier2D"/> during the playfield's collision detection.
    /// </summary>
    void OnHitBarrier(Sprite2D self, Barrier2D barrier, in UpdateContext context) { }
}

/// <summary>
/// Forwards playfield-detected hits to a sprite's <see cref="IHitHandler2D"/>
/// behaviors. Collision dispatch lives with the playfield, not on the sprite.
/// </summary>
internal static class HitDispatch2D
{
    public static void SpriteHit(Sprite2D self, Sprite2D other, in UpdateContext context)
    {
        var behaviors = self.Behaviors;
        for (int i = 0; i < behaviors.Count; i++)
            if (behaviors[i] is IHitHandler2D handler)
                handler.OnHitSprite(self, other, in context);
    }

    public static void BarrierHit(Sprite2D self, Barrier2D barrier, in UpdateContext context)
    {
        var behaviors = self.Behaviors;
        for (int i = 0; i < behaviors.Count; i++)
            if (behaviors[i] is IHitHandler2D handler)
                handler.OnHitBarrier(self, barrier, in context);
    }
}
