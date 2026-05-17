namespace Blitter.Blocks;

/// <summary>
/// Passive collision responder: fires <see cref="OnHit"/> whenever the
/// host sprite's <see cref="Prop2D.HitCircle"/> overlaps another prop's
/// during the owning container's collision pass.
/// </summary>
public sealed class HitResponder2D : SpriteBehavior2D
{
    /// <summary>
    /// Invoked for each prop the host sprite overlaps this tick. First
    /// argument is the host sprite; second is the other prop.
    /// </summary>
    public Action<Sprite2D, Prop2D>? OnHit { get; set; }

    public override void Update(Sprite2D target, in UpdateContext2D context)
    {
    }

    public override void OnCollision(Sprite2D self, Prop2D other, in UpdateContext2D context)
        => OnHit?.Invoke(self, other);
}
