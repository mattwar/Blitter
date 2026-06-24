namespace Blitter.Blocks3D;

/// <summary>
/// Capability for a <see cref="Behavior"/> that responds to collisions the
/// <see cref="PlayField3D"/> detects. The playfield invokes these on each
/// behavior of a sprite involved in a hit; the sprite itself exposes no
/// collision API. Implement only the method you need — both default to no-ops.
/// </summary>
public interface IHittable3D
{
    /// <summary>
    /// Invoked when <paramref name="self"/>'s <see cref="Sprite3D.HitSphere"/>
    /// overlaps another sprite's during the playfield's collision pass.
    /// </summary>
    void OnHitSprite(Sprite3D self, Sprite3D other, in UpdateContext context) { }

    /// <summary>
    /// Invoked when <paramref name="self"/>'s <see cref="Sprite3D.HitSphere"/>
    /// overlaps a <see cref="Barrier3D"/> during the playfield's collision pass.
    /// </summary>
    void OnHitBarrier(Sprite3D self, Barrier3D barrier, in UpdateContext context) { }
}

/// <summary>
/// Forwards playfield-detected hits to a sprite's <see cref="IHittable3D"/>
/// behaviors. Collision dispatch lives with the playfield, not on the sprite.
/// </summary>
internal static class HitDispatch3D
{
    public static void SpriteHit(Sprite3D self, Sprite3D other, in UpdateContext context)
    {
        var behaviors = self.Behaviors;
        for (int i = 0; i < behaviors.Count; i++)
            if (behaviors[i] is IHittable3D handler)
                handler.OnHitSprite(self, other, in context);
    }

    public static void BarrierHit(Sprite3D self, Barrier3D barrier, in UpdateContext context)
    {
        var behaviors = self.Behaviors;
        for (int i = 0; i < behaviors.Count; i++)
            if (behaviors[i] is IHittable3D handler)
                handler.OnHitBarrier(self, barrier, in context);
    }
}
