namespace Blitter.Blocks;

/// <summary>
/// A behavior that controls a <see cref="Sprite2D"/>.
/// </summary>
public abstract class SpriteBehavior2D : Behavior2D
{
    /// <summary>
    /// Apply this behavior to <paramref name="target"/> for one frame.
    /// </summary>
    public virtual void Apply(Sprite2D target, in UpdateContext2D context) {}

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
