namespace Blitter.Blocks;

/// <summary>
/// Fades the host sprite's <see cref="Sprite2D.Tint"/> alpha to zero
/// over <see cref="Duration"/>, then sets <see cref="Sprite2D.IsAlive"/> to <c>false</c> so the playfield
/// reaps it. Useful for transient effects like score popups, debris, and impact flashes.
/// </summary>
public sealed class FadeAndExpire2D : SpriteBehavior2D
{
    /// <summary>Total lifetime over which the alpha ramps to zero.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(1);

    public override void Update(Sprite2D target, in UpdateContext2D context)
    {
        if (Duration <= TimeSpan.Zero)
        {
            target.IsAlive = false;
            return;
        }

        var t = (float)(target.Age.TotalSeconds / Duration.TotalSeconds);
        if (t >= 1f)
        {
            target.IsAlive = false;
            return;
        }

        var tint = target.Tint;
        byte a = (byte)Math.Clamp((1f - t) * 255f, 0f, 255f);
        target.Tint = new Color(tint.R, tint.G, tint.B, a);
    }
}
