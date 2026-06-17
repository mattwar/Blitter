namespace Blitter.Blocks3D;

/// <summary>
/// A behavior that controls a <see cref="Sprite3D"/>.
/// </summary>
public abstract class SpriteBehavior3D : Behavior3D
{
    /// <summary>
    /// Invoked when the host sprite's <see cref="Sprite3D.HitSphere"/>
    /// overlaps another sprite's during the playfield's collision pass.
    /// </summary>
    public virtual void OnHitSprite(Sprite3D self, Sprite3D other, in UpdateContext context) { }

    /// <summary>
    /// Invoked when the host sprite's <see cref="Sprite3D.HitSphere"/>
    /// overlaps a <see cref="Barrier3D"/> during the playfield's collision pass.
    /// </summary>
    public virtual void OnHitBarrier(Sprite3D self, Barrier3D barrier, in UpdateContext context) { }
}
