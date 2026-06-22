using System.Numerics;

namespace Blitter.Blocks2D;

/// <summary>
/// Scene layer that draws short-lived drifting text labels.
/// Use for score popups, damage numbers, pickup names, status callouts.
/// </summary>
public sealed class FloatingTextLayer2D : Layer2D
{
    /// <summary>Font all popups are drawn with.</summary>
    public required Font Font { get; init; }

    /// <summary>
    /// When true, popups are screen-locked: the layer detaches the camera while drawing. 
    /// When false (default), popups live in world space and scroll with the camera.
    /// </summary>
    public bool ScreenSpace { get; init; }

    /// <summary>
    /// Default drift velocity when callers omit it. Pixels/sec.
    /// </summary>
    public Vector2 DefaultVelocity { get; set; } = new(0f, -90f);

    /// <summary>
    /// Default lifetime of floating items, when callers omit it.
    /// </summary>
    public TimeSpan DefaultLifetime { get; set; } = TimeSpan.FromSeconds(1.2);

    /// <summary>
    /// Hard cap on simultaneously-active popups. 
    /// Oldest are dropped in favor of the newer ones.
    /// </summary>
    public int MaxItems { get; set; } = 256;

    private readonly List<Item> _items = new();

    private struct Item
    {
        public string Text;
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
        public float Scale;
        public TimeSpan Lifetime;
        public TimeSpan Age;
    }

    /// <summary>
    /// Spawns a popup floating popup.
    /// The <paramref name="position"/> is world-space when <see cref="ScreenSpace"/> is false, otherwise screen-space.
    /// </summary>
    public void Add(
        string text,
        Vector2 position,
        Color color,
        float scale = 1f,
        Vector2? velocity = null,
        TimeSpan? lifetime = null)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (_items.Count >= MaxItems)
            _items.RemoveAt(0);
        _items.Add(new Item
        {
            Text = text,
            Position = position,
            Velocity = velocity ?? DefaultVelocity,
            Color = color,
            Scale = scale,
            Lifetime = lifetime ?? DefaultLifetime,
            Age = TimeSpan.Zero,
        });
    }

    /// <summary>
    /// Clears all floating texts.
    /// </summary>
    public void Clear() => _items.Clear();

    /// <summary>
    /// The number of active floating texts.
    /// </summary>
    public int Count => _items.Count;

    public override void Update(in UpdateContext context)
    {
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f) return;
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var it = _items[i];
            it.Age += context.ElapsedSinceLastUpdate;
            if (it.Age >= it.Lifetime)
            {
                _items.RemoveAt(i);
                continue;
            }
            it.Position += it.Velocity * dt;
            _items[i] = it;
        }
    }

    protected override void DrawContent(Renderer2D renderer)
    {
        if (_items.Count == 0) return;

        using var _ = renderer.PushState();
        if (ScreenSpace)
            renderer.Camera = null;

        var (baseScaleX, baseScaleY) = renderer.Scale;
        foreach (var it in _items)
        {
            float t = (float)(it.Age.TotalSeconds / it.Lifetime.TotalSeconds);
            byte a = (byte)Math.Clamp((1f - t) * it.Color.A, 0, 255);
            var color = new Color(it.Color.R, it.Color.G, it.Color.B, a);

            if (it.Scale == 1f)
            {
                Font.DrawText(renderer, it.Text, color, it.Position.X, it.Position.Y);
            }
            else
            {
                // Renderer2D.Scale multiplies both coordinates and
                // sizes, so to draw at unscaled position p at scale s
                // we set the scale, divide the position, restore after.
                renderer.Scale = (baseScaleX * it.Scale, baseScaleY * it.Scale);
                Font.DrawText(renderer, it.Text, color,
                    it.Position.X / it.Scale, it.Position.Y / it.Scale);
                renderer.Scale = (baseScaleX, baseScaleY);
            }
        }
    }
}
