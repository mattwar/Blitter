using System.Numerics;

namespace Blitter.Blocks2D;

/// <summary>
/// Anchored HUD score readout. Owns the running total via
/// <see cref="Score"/>, briefly pulses when the value changes, and
/// (optionally) forwards a "+N" popup to a paired
/// <see cref="FloatingTextLayer2D"/>.
/// </summary>
public sealed class ScoreLayer2D : Entity, IDrawable2D, IUpdatable
{
    /// <summary>Font used for the HUD readout.</summary>
    public required Font Font { get; init; }

    /// <summary>Where the readout sits relative to the viewport.</summary>
    public HudAnchor Anchor { get; set; } = HudAnchor.TopLeft;

    /// <summary>
    /// Pixel offset from the chosen <see cref="Anchor"/>. Positive
    /// values push toward the center: e.g. with
    /// <see cref="HudAnchor.TopRight"/>, X inset from the right edge,
    /// Y inset from the top.
    /// </summary>
    public Vector2 Offset { get; set; } = new(20f, 20f);

    /// <summary>Text color.</summary>
    public Color Color { get; set; } = Color.White;

    /// <summary>Formats the score for display. Default emits <c>"SCORE 1,234"</c>.</summary>
    public Func<long, string> Format { get; set; } = v => $"SCORE {v:N0}";

    /// <summary>How long the pulse lasts after a score change.</summary>
    public TimeSpan PulseDuration { get; set; } = TimeSpan.FromMilliseconds(220);

    /// <summary>Peak scale multiplier at the start of the pulse.</summary>
    public float PulseScale { get; set; } = 1.25f;

    /// <summary>
    /// Optional companion layer that receives a "+points" popup whenever
    /// <see cref="Add(long, Vector2)"/> is called.
    /// </summary>
    public FloatingTextLayer2D? Popups { get; set; }

    /// <summary>Default popup color when <see cref="Add(long, Vector2)"/> is called for a positive delta.</summary>
    public Color PositivePopupColor { get; set; } = new Color(255, 215, 0);

    /// <summary>Default popup color for a negative delta.</summary>
    public Color NegativePopupColor { get; set; } = new Color(255, 90, 90);

    /// <summary>Lifetime/velocity overrides for spawned popups; nulls fall through to the popup layer's defaults.</summary>
    public TimeSpan? PopupLifetime { get; set; }
    public Vector2? PopupVelocity { get; set; }
    public float PopupScale { get; set; } = 1f;

    /// <summary>Current score.</summary>
    public long Score { get; private set; }

    /// <summary>Fires after <see cref="Score"/> changes, with (oldValue, newValue).</summary>
    public event Action<long, long>? Changed;

    // Elapsed since the most recent score change; drives the pulse.
    private TimeSpan _sincePulse = TimeSpan.MaxValue;

    /// <summary>Adds <paramref name="points"/> to <see cref="Score"/>. No popup.</summary>
    public void Add(long points)
    {
        if (points == 0) return;
        var old = Score;
        Score += points;
        _sincePulse = TimeSpan.Zero;
        Changed?.Invoke(old, Score);
    }

    /// <summary>
    /// Adds <paramref name="points"/> and (if a <see cref="Popups"/>
    /// layer is set) spawns a "+N" / "-N" popup at
    /// <paramref name="popupPosition"/>.
    /// </summary>
    public void Add(long points, Vector2 popupPosition)
    {
        if (points == 0) return;
        Add(points);
        if (Popups is { } popups)
        {
            var text = points >= 0 ? $"+{points:N0}" : $"-{-points:N0}";
            var color = points >= 0 ? PositivePopupColor : NegativePopupColor;
            popups.Add(text, popupPosition, color, PopupScale, PopupVelocity, PopupLifetime);
        }
    }

    /// <summary>Resets the score to zero (or a chosen baseline) without firing a pulse.</summary>
    public void Reset(long to = 0)
    {
        if (Score == to) return;
        var old = Score;
        Score = to;
        Changed?.Invoke(old, Score);
    }

    public void Update(in EntityUpdateContext context)
    {
        if (_sincePulse < PulseDuration)
            _sincePulse += context.ElapsedSinceLastUpdate;
    }

    public void Draw(Renderer2D renderer)
    {
        using var _ = renderer.PushState();
        renderer.Camera = null;

        var text = Format(Score);
        var size = Font.Measure(text);

        // Pulse: ease the scale back from PulseScale → 1 over PulseDuration.
        float scale = 1f;
        if (_sincePulse < PulseDuration && PulseScale > 1f && PulseDuration > TimeSpan.Zero)
        {
            float t = (float)(_sincePulse.TotalSeconds / PulseDuration.TotalSeconds);
            // ease-out: 1 - (1 - t)^2
            float eased = 1f - (1f - t) * (1f - t);
            scale = PulseScale + (1f - PulseScale) * eased;
        }

        // Resolve anchor using the *visible* (scaled) text size so
        // pulsing doesn't push the text off-anchor.
        var viewport = new Vector2(
            renderer.LogicalSize.Width != 0 ? renderer.LogicalSize.Width : renderer.OutputSize.Width,
            renderer.LogicalSize.Height != 0 ? renderer.LogicalSize.Height : renderer.OutputSize.Height);
        var origin = Anchor.ResolveOrigin(viewport, size * scale, Offset);

        if (scale == 1f)
        {
            Font.DrawText(renderer, text, Color, origin.X, origin.Y);
        }
        else
        {
            var (sx, sy) = renderer.Scale;
            renderer.Scale = (sx * scale, sy * scale);
            Font.DrawText(renderer, text, Color, origin.X / scale, origin.Y / scale);
            renderer.Scale = (sx, sy);
        }
    }
}
