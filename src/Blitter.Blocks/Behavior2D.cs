namespace Blitter.Blocks;

/// <summary>
/// A behavior for an element of a <see cref="Scene2D"/>
/// </summary>
public abstract class Behavior2D
{
    /// <summary>
    /// When false the host skips this behavior's update for the frame.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// A behavior that controlls a <see cref="Sprite2D"/>.
/// </summary>
public abstract class SpriteBehavior2D : Behavior2D
{
    /// <summary>
    /// Advance the target sprite by one tick.
    /// </summary>
    public abstract void Update(Sprite2D target, in UpdateContext2D context);

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
