namespace Blitter.Blocks2D;

/// <summary>
/// Per-frame callback for a <see cref="CustomSpriteBehavior2D"/>.
/// </summary>
public delegate void SpriteApplier(Sprite2D target, in UpdateContext2D context);

/// <summary>
/// A <see cref="SpriteBehavior2D"/> that delegates its per-frame work to supplied callbacks.
/// </summary>
public sealed class CustomSpriteBehavior2D : SpriteBehavior2D
{
    public CustomSpriteBehavior2D()
    {
    }

    public SpriteApplier? OnApply { get; set; }

    /// <summary>Invoked for each sprite the host overlaps this frame.</summary>
    public Action<Sprite2D, Sprite2D>? OnSpriteHit { get; set; }

    /// <summary>Invoked for each barrier the host overlaps this frame.</summary>
    public Action<Sprite2D, Barrier2D>? OnBarrierHit { get; set; }

    public override void Apply(Sprite2D target, in UpdateContext2D context)
        => OnApply?.Invoke(target, in context);

    public override void OnHitSprite(Sprite2D self, Sprite2D other, in UpdateContext2D context)
        => OnSpriteHit?.Invoke(self, other);

    public override void OnHitBarrier(Sprite2D self, Barrier2D barrier, in UpdateContext2D context)
        => OnBarrierHit?.Invoke(self, barrier);
}
