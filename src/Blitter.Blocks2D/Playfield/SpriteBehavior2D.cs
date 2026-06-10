namespace Blitter.Blocks2D;

/// <summary>
/// A behavior that controls a <see cref="Sprite2D"/>.
/// </summary>
public abstract class SpriteBehavior2D : Behavior2D
{
    /// <summary>
    /// Called once after the scene tree is built but before the first
    /// frame, with <paramref name="self"/> being the sprite this behavior
    /// is attached to. Resolve dependencies on other nodes via
    /// <c>self.Scene.Find…</c> here and cache them. The default does
    /// nothing.
    /// </summary>
    protected internal virtual void OnAttach(Sprite2D self)
    {
    }

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
