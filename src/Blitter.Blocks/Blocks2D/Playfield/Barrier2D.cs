namespace Blitter.Blocks2D;

/// <summary>
/// An obstacle in a <see cref="PlayField2D"/>.
/// Barriers collide with sprites, but not each other.
/// </summary>
public abstract class Barrier2D : Entity
{
    /// <summary>
    /// Collision shape of this barrier in world space.
    /// </summary>
    public abstract PosedHitShape2D HitShape { get; }

    /// <summary>
    /// Render this barrier.
    /// By default, barriers don't have a visual representation.
    /// </summary>
    public virtual void Draw(Renderer2D renderer) { }

    /// <summary>
    /// Called when the <paramref name="hitter"/> collided with this barrier.
    /// </summary>
    public virtual void OnHitSprite(Sprite2D hitter, in UpdateContext context) { }

    /// <summary>
    /// Physical characteristics of this barrier.
    /// </summary>
    public virtual PhysicsMaterial PhysicsMaterial { get; set; } = PhysicsMaterial.Ideal;

    /// <summary>
    /// Surface velocity at <paramref name="point"/> in world units per second. 
    /// </summary>
    public virtual System.Numerics.Vector2 SurfaceVelocityAt(System.Numerics.Vector2 point)
        => System.Numerics.Vector2.Zero;
}
