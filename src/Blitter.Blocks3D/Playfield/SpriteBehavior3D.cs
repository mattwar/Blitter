namespace Blitter.Blocks3D;

/// <summary>
/// A behavior that controls a <see cref="Sprite3D"/>.
/// </summary>
public abstract class SpriteBehavior3D : Behavior3D
{
    /// <summary>Apply this behavior to <paramref name="target"/> for one frame.</summary>
    public virtual void Apply(Sprite3D target, in UpdateContext3D context) { }

    /// <summary>
    /// Invoked when the host sprite's <see cref="Sprite3D.HitSphere"/>
    /// overlaps another sprite's during the playfield's collision pass.
    /// </summary>
    public virtual void OnHitSprite(Sprite3D self, Sprite3D other, in UpdateContext3D context) { }

    /// <summary>
    /// Invoked when the host sprite's <see cref="Sprite3D.HitSphere"/>
    /// overlaps a <see cref="Barrier3D"/> during the playfield's collision pass.
    /// </summary>
    public virtual void OnHitBarrier(Sprite3D self, Barrier3D barrier, in UpdateContext3D context) { }
}
