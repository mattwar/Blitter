using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Convenience shape draw methods for <see cref="Renderer2D"/> built on
/// top of <see cref="Renderer2D.DrawGeometry(ReadOnlySpan{Vertex2D}, ReadOnlySpan{int}, Texture2D?)"/>.
/// </summary>
public static class Renderer2DDrawExtensions
{
    /// <summary>
    /// Draws a line segment with thickness by emitting a quad.
    /// </summary>
    public static bool DrawThickLine(
        this Renderer2D renderer,
        Vector2 a,
        Vector2 b,
        Color color,
        float thickness)
    {
        var d = b - a;
        var len = d.Length();
        if (len <= float.Epsilon)
            return false;

        var n = new Vector2(-d.Y, d.X) / len;
        var h = thickness * 0.5f;

        Span<Vertex2D> verts =
        [
            new(a + n * h, color),
            new(b + n * h, color),
            new(b - n * h, color),
            new(a - n * h, color),
        ];

        Span<int> idx = [0, 1, 2, 0, 2, 3];
        return renderer.DrawGeometry(verts, idx);
    }

    /// <summary>
    /// Draws a filled disc as a triangle fan.
    /// </summary>
    /// <remarks>
    /// The center vertex is brightened slightly from <paramref name="color"/>
    /// so the fan interpolates into a subtle radial highlight.
    /// </remarks>
    public static bool DrawDisc(
        this Renderer2D renderer,
        Vector2 center,
        float radius,
        Color color,
        int segments = 36)
    {
        if (radius <= 0f)
            return false;

        if (segments < 3)
            segments = 3;

        var hi = new Color(
            (byte)Math.Min(255, color.R + 80),
            (byte)Math.Min(255, color.G + 80),
            (byte)Math.Min(255, color.B + 80));

        var vertices = new Vertex2D[segments + 1];
        vertices[0] = new Vertex2D(center, hi);

        for (int i = 0; i < segments; i++)
        {
            var theta = i * (MathF.PI * 2f / segments);
            var p = center + new Vector2(MathF.Cos(theta), MathF.Sin(theta)) * radius;
            vertices[i + 1] = new Vertex2D(p, color);
        }

        var indices = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            indices[i * 3] = 0;
            indices[i * 3 + 1] = 1 + i;
            indices[i * 3 + 2] = 1 + ((i + 1) % segments);
        }

        return renderer.DrawGeometry(vertices, indices);
    }
}