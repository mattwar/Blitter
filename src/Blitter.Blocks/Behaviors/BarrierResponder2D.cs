namespace Blitter.Blocks;

/// <summary>
/// Passive barrier responder: fires <see cref="OnHit"/> whenever the
/// host sprite's <see cref="Sprite2D.HitCircle"/> overlaps a
/// <see cref="Barrier2D"/> during the playfield's collision pass.
/// </summary>
public sealed class BarrierResponder2D : SpriteBehavior2D
{
    /// <summary>
    /// Invoked for each barrier the host sprite overlaps this tick.
    /// First argument is the host sprite; second is the barrier.
    /// </summary>
    public Action<Sprite2D, Barrier2D>? OnHit { get; set; }

    public override void Update(Sprite2D target, in UpdateContext2D context)
    {
    }

    public override void OnHitBarrier(Sprite2D self, Barrier2D barrier, in UpdateContext2D context)
        => OnHit?.Invoke(self, barrier);
}
