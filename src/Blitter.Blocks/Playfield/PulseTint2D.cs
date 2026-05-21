namespace Blitter.Blocks;

/// <summary>
/// Smoothly cycles the host sprite's <see cref="Sprite2D.Tint"/>
/// between <see cref="Low"/> and <see cref="High"/> on a sine wave
/// with the given <see cref="Period"/>. Useful for radioactive /
/// hazard markers, charged power-ups, and "hold to interact" prompts.
/// </summary>
public sealed class PulseTint2D : SpriteBehavior2D
{
    /// <summary>
    /// Tint at the trough/bottom of the pulse (t = 0).
    /// </summary>
    public required Color Low { get; init; }

    /// <summary>
    /// Tint at the crest/top of the pulse (t = 1).
    /// </summary>
    public required Color High { get; init; }

    /// <summary>
    /// Time for one full Low → High → Low cycle.
    /// </summary>
    public TimeSpan Period { get; init; } = TimeSpan.FromSeconds(1);

    public override void Apply(Sprite2D target, in UpdateContext2D context)
    {
        var seconds = Period.TotalSeconds;
        if (seconds <= 0)
            return;

        // 0..1 triangle-shaped weight from a sine, driven by sprite age
        // so each sprite pulses independently (good for spawn variety).
        var phase = target.Age.TotalSeconds / seconds;
        var t = 0.5f + 0.5f * MathF.Sin((float)(phase * Math.Tau));
        target.Tint = Color.Lerp(Low, High, t);
    }

    /// <summary>
    /// Pulses brightness around <paramref name="baseColor"/>
    /// </summary>
    public static PulseTint2D FromBrightness(Color baseColor, float amount, TimeSpan period)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return new PulseTint2D
        {
            Low = Color.Lerp(baseColor, new Color(0, 0, 0, baseColor.A), amount),
            High = Color.Lerp(baseColor, new Color(255, 255, 255, baseColor.A), amount),
            Period = period,
        };
    }

    /// <summary>
    /// Pulses only the alpha channel of <paramref name="color"/>
    /// </summary>
    public static PulseTint2D FromAlpha(Color color, byte minAlpha, TimeSpan period)
    {
        if (minAlpha > color.A)
            minAlpha = color.A;
        return new PulseTint2D
        {
            Low = new Color(color.R, color.G, color.B, minAlpha),
            High = color,
            Period = period,
        };
    }
}
