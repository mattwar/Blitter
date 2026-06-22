namespace Blitter.Blocks2D;
using Bits;

/// <summary>
/// Per-frame callback for a <see cref="CustomSpriteBehavior2D"/>.
/// </summary>
public delegate void SpriteApplier(Sprite2D target, in UpdateContext context);

/// <summary>
/// A <see cref="Behavior"/> that delegates its per-frame work and hit
/// handling to supplied callbacks.
/// </summary>
public sealed class CustomSpriteBehavior2D : Behavior, IHitHandler2D
{
    public CustomSpriteBehavior2D()
    {
    }

    public SpriteApplier? OnApply { get; set; }

    /// <summary>Invoked for each sprite the host overlaps this frame.</summary>
    public Action<Sprite2D, Sprite2D>? OnSpriteHit { get; set; }

    /// <summary>Invoked for each barrier the host overlaps this frame.</summary>
    public Action<Sprite2D, Barrier2D>? OnBarrierHit { get; set; }

    public override void Apply(in UpdateContext context)
    {
        if (this.Entity is Sprite2D sprite)
            OnApply?.Invoke(sprite, in context);
    }

    public void OnHitSprite(Sprite2D self, Sprite2D other, in UpdateContext context)
        => OnSpriteHit?.Invoke(self, other);

    public void OnHitBarrier(Sprite2D self, Barrier2D barrier, in UpdateContext context)
        => OnBarrierHit?.Invoke(self, barrier);
}
