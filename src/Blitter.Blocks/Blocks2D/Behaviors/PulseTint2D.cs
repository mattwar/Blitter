namespace Blitter.Blocks2D;

/// <summary>
/// Smoothly cycles the host's <see cref="Appearance2D.Tint"/>
/// between <see cref="Low"/> and <see cref="High"/> on a sine wave
/// with the given <see cref="Period"/>. Useful for radioactive /
/// hazard markers, charged power-ups, and "hold to interact" prompts.
/// </summary>
public sealed class PulseTint2D : Behavior, IUpdatable
{
    /// <summary>
    /// Tint at the trough/bottom of the pulse (t = 0).
    /// </summary>
    public Color Low { get; set; } = Color.White;

    /// <summary>
    /// Tint at the crest/top of the pulse (t = 1).
    /// </summary>
    public Color High { get; set; } = Color.White;

    /// <summary>
    /// Time for one full Low → High → Low cycle.
    /// </summary>
    public TimeSpan Period { get; set; } = TimeSpan.FromSeconds(1);

    private Appearance2D _appearance = null!;

    // Time accumulated since attach; drives the phase so each instance
    // pulses independently from when it started (good for spawn variety).
    private TimeSpan _elapsed;

    protected override void OnAttach(IEntity entity)
    {
        _appearance = entity.GetOrAddTrait<Appearance2D>();
    }

    public void Update(in UpdateContext context)
    {
        var seconds = Period.TotalSeconds;
        if (seconds <= 0)
            return;

        _elapsed += context.ElapsedSinceLastUpdate;

        // 0..1 triangle-shaped weight from a sine, driven by accumulated
        // age so each instance pulses independently (good for spawn variety).
        var phase = _elapsed.TotalSeconds / seconds;
        var t = 0.5f + 0.5f * MathF.Sin((float)(phase * Math.Tau));
        _appearance.Tint = Color.Lerp(Low, High, t);
    }

    /// <summary>
    /// Pulses brightness around <paramref name="baseColor"/>
    /// </summary>
    public static PulseTint2D FromBrightness(Color baseColor, float amount, TimeSpan period)
    {
        return new PulseTint2D().SetBrightness(baseColor, amount, period);
    }

    /// <summary>
    /// Configures this behavior to pulse brightness around <paramref name="baseColor"/>.
    /// </summary>
    public PulseTint2D SetBrightness(Color baseColor, float amount, TimeSpan period)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        Low = Color.Lerp(baseColor, new Color(0, 0, 0, baseColor.A), amount);
        High = Color.Lerp(baseColor, new Color(255, 255, 255, baseColor.A), amount);
        Period = period;
        return this;
    }

    /// <summary>
    /// Pulses only the alpha channel of <paramref name="color"/>
    /// </summary>
    public static PulseTint2D FromAlpha(Color color, byte minAlpha, TimeSpan period)
    {
        return new PulseTint2D().SetAlpha(color, minAlpha, period);
    }

    /// <summary>
    /// Configures this behavior to pulse only the alpha channel of <paramref name="color"/>.
    /// </summary>
    public PulseTint2D SetAlpha(Color color, byte minAlpha, TimeSpan period)
    {
        if (minAlpha > color.A)
            minAlpha = color.A;
        Low = new Color(color.R, color.G, color.B, minAlpha);
        High = color;
        Period = period;
        return this;
    }
}
