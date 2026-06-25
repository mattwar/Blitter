namespace Blitter.Blocks2D;

/// <summary>
/// Fades the host entity's <see cref="Appearance2D.Tint"/> alpha to zero over
/// <see cref="Duration"/>, then removes the entity from its container. Works on
/// any entity: the fade applies when the entity carries an
/// <see cref="Appearance2D"/> trait, and expiry happens on schedule regardless.
/// Useful for transient effects like score popups, debris, and impact flashes.
/// </summary>
public sealed class FadeAndExpire2D : Behavior, IUpdatable
{
    /// <summary>Total lifetime over which the alpha ramps to zero.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(1);

    public void Update(in UpdateContext context)
    {
        var entity = this.Entity;
        var age = entity.Age();

        if (Duration <= TimeSpan.Zero || age >= Duration)
        {
            entity.RemoveFromContainer();
            return;
        }

        if (entity.TryGetTrait<Appearance2D>(out var appearance))
        {
            var t = (float)(age.TotalSeconds / Duration.TotalSeconds);
            var tint = appearance.Tint;
            byte a = (byte)Math.Clamp((1f - t) * 255f, 0f, 255f);
            appearance.Tint = new Color(tint.R, tint.G, tint.B, a);
        }
    }
}
