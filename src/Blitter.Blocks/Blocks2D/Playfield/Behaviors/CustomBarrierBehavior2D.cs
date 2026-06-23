namespace Blitter.Blocks2D;

/// <summary>
/// A <see cref="Behavior"/> that delegates a barrier's sprite-hit handling to a
/// supplied callback. Barrier-side analog of <see cref="CustomSpriteBehavior2D"/>:
/// attach it so a barrier reacts to sprite contacts without subclassing a hit
/// method onto the barrier itself.
/// </summary>
public sealed class CustomBarrierBehavior2D : Behavior, IBarrierHitHandler2D
{
    /// <summary>Invoked for each sprite that hits the host barrier this frame.</summary>
    public Action<Barrier2D, Sprite2D>? OnSpriteHit { get; set; }

    public override void Apply(in UpdateContext context) { }

    public void OnHitSprite(Barrier2D self, Sprite2D sprite, in UpdateContext context)
        => OnSpriteHit?.Invoke(self, sprite);
}
