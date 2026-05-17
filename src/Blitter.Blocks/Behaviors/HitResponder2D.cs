namespace Blitter.Blocks;

/// <summary>
/// Passive collision responder: fires <see cref="OnHit"/> whenever the
/// host sprite's <see cref="Sprite2D.HitCircle"/> overlaps another
/// sprite's during the playfield's collision pass.
/// </summary>
public sealed class HitResponder2D : SpriteBehavior2D
{
    /// <summary>
    /// Invoked for each sprite the host sprite overlaps this tick.
    /// First argument is the host sprite; second is the other sprite.
    /// </summary>
    public Action<Sprite2D, Sprite2D>? OnHit { get; set; }

    public override void Update(Sprite2D target, in UpdateContext2D context)
    {
    }

    public override void OnHitSprite(Sprite2D self, Sprite2D other, in UpdateContext2D context)
        => OnHit?.Invoke(self, other);
}
