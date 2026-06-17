namespace Blitter.Blocks2D;

/// <summary>
/// Fades the host sprite's <see cref="Sprite2D.Tint"/> alpha to zero
/// over <see cref="Duration"/>, then sets <see cref="Sprite2D.IsAlive"/> to <c>false</c> so the playfield
/// reaps it. Useful for transient effects like score popups, debris, and impact flashes.
/// </summary>
public sealed class FadeAndExpire2D : SpriteBehavior2D
{
    /// <summary>Total lifetime over which the alpha ramps to zero.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(1);

    public override void Apply(in UpdateContext context)
    {
        if (this.Entity is Sprite2D sprite)
        {
            if (Duration <= TimeSpan.Zero)
            {
                sprite.IsAlive = false;
                return;
            }

            var t = (float)(sprite.Age.TotalSeconds / Duration.TotalSeconds);
            if (t >= 1f)
            {
                sprite.IsAlive = false;
                return;
            }

            var tint = sprite.Tint;
            byte a = (byte)Math.Clamp((1f - t) * 255f, 0f, 255f);
            sprite.Tint = new Color(tint.R, tint.G, tint.B, a);
        }         
    }
}
