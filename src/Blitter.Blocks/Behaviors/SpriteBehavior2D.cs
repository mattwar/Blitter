namespace Blitter.Blocks;

/// <summary>
/// A behavior that controls a <see cref="Sprite2D"/>.
/// </summary>
public abstract class SpriteBehavior2D : Behavior2D
{
    /// <summary>
    /// Advance the target sprite by one tick.
    /// </summary>
    public virtual void Update(Sprite2D target, in UpdateContext2D context) {}

    /// <summary>
    /// Invoked when the host sprite's <see cref="Sprite2D.HitCircle"/>
    /// overlaps another sprite's during the playfield's collision detection.
    /// </summary>
    public virtual void OnHitSprite(Sprite2D self, Sprite2D other, in UpdateContext2D context) { }

    /// <summary>
    /// Invoked when the host sprite's <see cref="Sprite2D.HitCircle"/>
    /// overlaps a <see cref="Barrier2D"/> during the playfield's collision detection.
    /// </summary>
    public virtual void OnHitBarrier(Sprite2D self, Barrier2D barrier, in UpdateContext2D context) { }
}
