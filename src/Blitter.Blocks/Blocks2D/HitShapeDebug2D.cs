using System.Numerics;

namespace Blitter.Blocks2D;

/// <summary>
/// Draws 2D hit-shape primitives as world-space debug outlines.
/// </summary>
public sealed class HitShapeDebug2D
{
    private readonly HitPrimitiveAction2D _drawPrimitive;
    private Renderer2D _renderer = null!;

    public HitShapeDebug2D()
    {
        _drawPrimitive = DrawPrimitive;
    }

    /// <summary>Draws the live primitives of a posed hit shape.</summary>
    public void Draw(Renderer2D renderer, in PosedHitShape2D shape)
    {
        _renderer = renderer;
        shape.Visit(_drawPrimitive);
    }

    private void DrawPrimitive(in HitPrimitive2D primitive)
    {
        switch (primitive.Kind)
        {
            case HitKind2D.Circle:
                DrawCircleOutline(_renderer, primitive.P0, primitive.R);
                break;
            case HitKind2D.Capsule:
                DrawCapsuleOutline(_renderer, primitive.P0, primitive.P1, primitive.R);
                break;
        }
    }

    public static void DrawCircleOutline(Renderer2D renderer, Vector2 center, float radius, int segments = 32)
    {
        Span<Vector2> pts = stackalloc Vector2[segments + 1];
        var step = MathF.Tau / segments;
        for (int i = 0; i <= segments; i++)
        {
            var a = i * step;
            pts[i] = new Vector2(center.X + MathF.Cos(a) * radius, center.Y + MathF.Sin(a) * radius);
        }
        renderer.DrawLines(pts);
    }

    public static void DrawCapsuleOutline(Renderer2D renderer, Vector2 a, Vector2 b, float radius, int capSegments = 12)
    {
        var axis = b - a;
        var len = axis.Length();
        if (len <= float.Epsilon)
        {
            DrawCircleOutline(renderer, a, radius, capSegments * 2);
            return;
        }

        var direction = axis / len;
        var normal = new Vector2(-direction.Y, direction.X) * radius;

        var totalPts = 1 + 1 + (capSegments + 1) + 1 + (capSegments + 1);
        Span<Vector2> pts = stackalloc Vector2[totalPts];
        int index = 0;
        pts[index++] = a + normal;
        pts[index++] = b + normal;

        var startB = MathF.Atan2(normal.Y, normal.X);
        for (int i = 1; i <= capSegments + 1; i++)
        {
            var angle = startB - MathF.PI * i / capSegments;
            pts[index++] = b + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        pts[index++] = a - normal;

        var startA = MathF.Atan2(-normal.Y, -normal.X);
        for (int i = 1; i <= capSegments + 1; i++)
        {
            var angle = startA - MathF.PI * i / capSegments;
            pts[index++] = a + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        renderer.DrawLines(pts);
    }
}
