using System.Numerics;

namespace Blitter.Blocks;

/// <summary>
/// A layer that scatters single-pixel stars across a rectangular region. 
/// Combine multiple instances at different <see cref="Layer2D.ParallaxFactor"/> values to build a parallax star field.
/// Default parallax is <c>(0, 0)</c> — screen-locked — which suits a space backdrop; 
/// raise it for nearer layers that drift with the camera.
/// </summary>
public class StarField2D : Layer2D
{
    private readonly Vector2[] _positions;
    private readonly byte[] _brightness;

    /// <summary>Color modulated by each star's brightness.</summary>
    public Color StarColor { get; set; } = new Color(255, 255, 255, 255);

    /// <summary>
    /// Creates a star field with <paramref name="count"/> stars
    /// scattered uniformly inside <paramref name="bounds"/>.
    /// <paramref name="bounds"/> is in the layer's local coordinate
    /// space — i.e. after <see cref="Layer2D.ParallaxFactor"/> has been
    /// applied to the camera — not in foreground world coordinates.
    /// For a screen-locked field (parallax <c>(0, 0)</c>) pass a
    /// viewport-sized rect centered on the origin.
    /// </summary>
    public StarField2D(int count, Rect bounds, int seed = 0)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        ParallaxFactor = Vector2.Zero;

        _positions = new Vector2[count];
        _brightness = new byte[count];
        var rng = new Random(seed);
        for (int i = 0; i < count; i++)
        {
            _positions[i] = new Vector2(
                bounds.X + (float)rng.NextDouble() * bounds.Width,
                bounds.Y + (float)rng.NextDouble() * bounds.Height
                );
            // Bias toward dim stars; a few bright ones stand out.
            var t = (float)rng.NextDouble();
            _brightness[i] = (byte)(64 + (int)(t * t * 191));
        }
    }

    public override void Update(in UpdateContext2D context)
    {
        // Static field; no per-tick state.
    }

    protected override void DrawContent(Renderer2D renderer)
    {
        if (_positions.Length == 0)
            return;

        // Bucket by brightness so we can issue one DrawPoints per shade
        // rather than one DrawPoint per star.
        Span<int> counts = stackalloc int[Buckets];
        for (int i = 0; i < _brightness.Length; i++)
            counts[BucketOf(_brightness[i])]++;

        int maxBucket = 0;
        for (int b = 0; b < Buckets; b++)
            if (counts[b] > maxBucket)
                maxBucket = counts[b];
        if (maxBucket == 0)
            return;

        // One scratch buffer reused across buckets.
        Span<Vector2> scratch = maxBucket <= 256
            ? stackalloc Vector2[maxBucket]
            : new Vector2[maxBucket];

        using var _ = renderer.PushState();
        for (int b = 0; b < Buckets; b++)
        {
            if (counts[b] == 0)
                continue;
            var pts = scratch[..counts[b]];
            int w = 0;
            for (int i = 0; i < _positions.Length; i++)
            {
                if (BucketOf(_brightness[i]) == b)
                    pts[w++] = _positions[i];
            }
            renderer.DrawColor = ShadeFor(b);
            renderer.DrawPoints(pts);
        }
    }

    private static int BucketOf(byte brightness) =>
        Math.Min(Buckets - 1, brightness * Buckets / 256);

    private const int Buckets = 4;

    private Color ShadeFor(int bucket)
    {
        // Map bucket 0..N-1 to a brightness scalar in roughly (0.25, 1].
        var t = (bucket + 1) / (float)Buckets;
        return new Color(
            (byte)(StarColor.R * t),
            (byte)(StarColor.G * t),
            (byte)(StarColor.B * t),
            StarColor.A);
    }
}
