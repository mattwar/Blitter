using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Blitter.Blocks2D;

/// <summary>
/// Shapes a <see cref="MinimapLayer2D"/> can draw for a sprite marker.
/// </summary>
public enum MinimapShape
{
    /// <summary>Filled square, optionally rotated.</summary>
    Square,
    /// <summary>Filled circle (rotation ignored).</summary>
    Circle,
    /// <summary>Filled isoceles triangle pointing along <see cref="MinimapMarker.Rotation"/> (0 = up).</summary>
    Triangle,
}

/// <summary>
/// Per-entity minimap symbol spec returned by
/// <see cref="MinimapLayer2D.GetMarker"/>.
/// </summary>
/// <param name="Color">Fill color.</param>
/// <param name="Radius">Half-extent in minimap pixels (size scales linearly with this).</param>
/// <param name="Shape">Symbol to draw.</param>
/// <param name="Rotation">Orientation in degrees, 0 = pointing up. Used by <see cref="MinimapShape.Square"/> and <see cref="MinimapShape.Triangle"/>.</param>
public readonly record struct MinimapMarker(
    Color Color,
    float Radius = 2f,
    MinimapShape Shape = MinimapShape.Square,
    float Rotation = 0f);

/// <summary>
/// Screen-locked overhead view of a <see cref="PlayField2D"/>. Draws
/// a background panel, per-sprite markers selected by
/// <see cref="GetMarker"/>, and (optionally) an outline of the
/// active camera's viewport. The minimap does not scroll with the
/// world — the layer detaches the camera while drawing.
/// </summary>
public class MinimapLayer2D : Entity, IDrawable2D
{
    private PlayField2D? _source;
    private string? _sourceName;

    /// <summary>
    /// Optional name of the sibling <see cref="PlayField2D"/> to display. When
    /// unset, the minimap resolves the single sibling playfield by type.
    /// </summary>
    public string? SourceName
    {
        get => _sourceName;
        set
        {
            if (_sourceName == value)
                return;
            _sourceName = value;
            _source = null;
        }
    }

    /// <summary>Where on the screen the minimap is drawn, in renderer pixels.</summary>
    public Rect ScreenRect { get; set; }

    /// <summary>
    /// Slice of world space mapped onto <see cref="ScreenRect"/>. When
    /// null, falls back to <see cref="PlayField2D.WorldBounds"/>.
    /// </summary>
    public Rect? WorldRect { get; set; }

    /// <summary>Background fill behind the markers. Set alpha 0 to skip.</summary>
    public Color BackgroundColor { get; set; } = new Color(0, 0, 0, 160);

    /// <summary>Outline around <see cref="ScreenRect"/>. Set alpha 0 to skip.</summary>
    public Color BorderColor { get; set; } = new Color(255, 255, 255, 96);

    /// <summary>
    /// Optional camera used to draw the viewport outline. When unset, the
    /// minimap resolves the nearest <see cref="ICamera2D"/> capability in its
    /// entity tree.
    /// </summary>
    public Camera2D? ViewportCamera { get; set; }

    /// <summary>Logical viewport size in world units. Used when drawing the viewport outline.</summary>
    public Vector2 ViewportSize { get; set; }

    /// <summary>Outline color for the viewport rectangle. Set alpha 0 to skip.</summary>
    public Color ViewportColor { get; set; } = new Color(255, 255, 0, 180);

    /// <summary>Clip markers to <see cref="ScreenRect"/> so they can't bleed past the border.</summary>
    public bool ClipToBounds { get; set; } = true;

    public void Draw(Renderer2D renderer)
    {
        if (!TryResolveSource(out var source))
            return;

        var world = WorldRect ?? source.WorldBounds ?? ScreenRect;
        if (world.Width <= 0 || world.Height <= 0)
            return;

        using var _ = renderer.PushState();
        renderer.Camera = null;

        if (BackgroundColor.A != 0)
        {
            renderer.DrawColor = BackgroundColor;
            renderer.DrawFillRect(ScreenRect);
        }

        // Clip only the contents (markers + viewport box) so the
        // border drawn afterward isn't trimmed by the exclusive
        // right/bottom edges of the clip rect.
        if (ClipToBounds)
            renderer.ClipRect = ScreenRect;

        float sx = ScreenRect.Width / world.Width;
        float sy = ScreenRect.Height / world.Height;
        Vector2 ToMini(Vector2 worldPos) => new(
            ScreenRect.X + (worldPos.X - world.X) * sx,
            ScreenRect.Y + (worldPos.Y - world.Y) * sy);

        foreach (var sprite in source.Entities)
        {
            if (sprite is IColliderBarrier2D) continue;
            if (source.GetContainment(sprite) == Containment.Removing) continue;
            if (GetMarker(sprite) is not { } m) continue;
            if (m.Radius <= 0f || m.Color.A == 0) continue;
            if (!sprite.TryGetTrait<Transform2D>(out var transform)) continue;

            var c = ToMini(transform.Position);
            DrawMarker(renderer, c, m);
        }

        if (ResolveViewportCamera() is { } cam && ViewportColor.A != 0 && ViewportSize != Vector2.Zero)
        {
            var topLeft = ToMini(cam.Position - ViewportSize * 0.5f);
            var size = new Vector2(ViewportSize.X * sx, ViewportSize.Y * sy);
            renderer.DrawColor = ViewportColor;
            // Outline as four line segments (no filled rect — we want
            // the world visible underneath).
            renderer.DrawLines(stackalloc Vector2[]
            {
                new(topLeft.X,           topLeft.Y),
                new(topLeft.X + size.X,  topLeft.Y),
                new(topLeft.X + size.X,  topLeft.Y + size.Y),
                new(topLeft.X,           topLeft.Y + size.Y),
                new(topLeft.X,           topLeft.Y),
            });
        }

        if (BorderColor.A != 0)
        {
            // Drop the clip so the right/bottom edges of the outline
            // (which sit exactly on the clip boundary) aren't trimmed.
            if (ClipToBounds)
                renderer.ClipRect = null;
            renderer.DrawColor = BorderColor;
            renderer.DrawLines(stackalloc Vector2[]
            {
                new(ScreenRect.X,                     ScreenRect.Y),
                new(ScreenRect.X + ScreenRect.Width,  ScreenRect.Y),
                new(ScreenRect.X + ScreenRect.Width,  ScreenRect.Y + ScreenRect.Height),
                new(ScreenRect.X,                     ScreenRect.Y + ScreenRect.Height),
                new(ScreenRect.X,                     ScreenRect.Y),
            });
        }
    }

    /// <summary>
    /// Selects how an entity is drawn on the minimap, or returns <c>null</c>
    /// to hide it.
    /// </summary>
    protected virtual MinimapMarker? GetMarker(IEntity entity) => null;

    private bool TryResolveSource([NotNullWhen(true)] out PlayField2D? source)
    {
        if (_source is not null)
        {
            source = _source;
            return true;
        }

        if (Container is not { } container)
        {
            source = null;
            return false;
        }

        var found = container.TryGetEntity(_sourceName, out source);

        if (found)
            _source = source;

        return found;
    }

    private Camera2D? ResolveViewportCamera()
    {
        if (ViewportCamera is { } camera)
            return camera;

        return this.TryFindCapability<ICamera2D>(out var capability)
            ? capability.Camera
            : null;
    }

    private static void DrawMarker(Renderer2D renderer, Vector2 center, MinimapMarker m)
    {
        switch (m.Shape)
        {
            case MinimapShape.Square when m.Rotation == 0f:
                renderer.DrawColor = m.Color;
                renderer.DrawFillRect(new Rect(
                    center.X - m.Radius, center.Y - m.Radius,
                    m.Radius * 2f, m.Radius * 2f));
                return;

            case MinimapShape.Square:
                DrawQuad(renderer, center, m.Radius, m.Rotation, m.Color);
                return;

            case MinimapShape.Circle:
                DrawDisc(renderer, center, m.Radius, m.Color);
                return;

            case MinimapShape.Triangle:
                DrawTriangle(renderer, center, m.Radius, m.Rotation, m.Color);
                return;
        }
    }

    private static void DrawQuad(Renderer2D renderer, Vector2 center, float r, float rotationDeg, Color color)
    {
        var (sin, cos) = MathF.SinCos(rotationDeg * MathF.PI / 180f);
        Vector2 Rot(float x, float y) => center + new Vector2(x * cos - y * sin, x * sin + y * cos);
        Span<Vertex2D> verts = stackalloc Vertex2D[4]
        {
            new(Rot(-r, -r), color),
            new(Rot( r, -r), color),
            new(Rot( r,  r), color),
            new(Rot(-r,  r), color),
        };
        Span<int> idx = stackalloc int[] { 0, 1, 2, 0, 2, 3 };
        renderer.DrawGeometry(verts, idx);
    }

    private static void DrawTriangle(Renderer2D renderer, Vector2 center, float r, float rotationDeg, Color color)
    {
        // Isoceles arrow pointing along the heading: tip at +y in local
        // space (then -90° because screen y grows downward), base at -y.
        // Rotation 0 = pointing up on screen.
        var (sin, cos) = MathF.SinCos(rotationDeg * MathF.PI / 180f);
        Vector2 Rot(float x, float y) => center + new Vector2(x * cos - y * sin, x * sin + y * cos);
        Span<Vertex2D> verts = stackalloc Vertex2D[3]
        {
            new(Rot( 0f,         -r * 1.4f), color), // tip (up)
            new(Rot(-r * 0.9f,    r * 0.9f), color), // base left
            new(Rot( r * 0.9f,    r * 0.9f), color), // base right
        };
        Span<int> idx = stackalloc int[] { 0, 1, 2 };
        renderer.DrawGeometry(verts, idx);
    }

    private static void DrawDisc(Renderer2D renderer, Vector2 center, float r, Color color)
    {
        // Fan with a vertex count proportional to radius so tiny markers
        // don't waste verts and large markers stay round.
        int segments = Math.Clamp((int)MathF.Ceiling(r * 3f), 8, 24);
        Span<Vertex2D> verts = stackalloc Vertex2D[segments + 1];
        Span<int> idx = stackalloc int[segments * 3];
        verts[0] = new Vertex2D(center, color);
        float step = MathF.Tau / segments;
        for (int i = 0; i < segments; i++)
        {
            var (sin, cos) = MathF.SinCos(i * step);
            verts[i + 1] = new Vertex2D(center + new Vector2(cos * r, sin * r), color);
            idx[i * 3 + 0] = 0;
            idx[i * 3 + 1] = i + 1;
            idx[i * 3 + 2] = (i + 1) % segments + 1;
        }
        renderer.DrawGeometry(verts, idx);
    }
}
