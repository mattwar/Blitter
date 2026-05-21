using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks;

/// <summary>
/// A non-interactive <see cref="Sprite2D"/> that renders a string with
/// a <see cref="Font"/>. Useful for score popups, floating labels,
/// damage numbers, and other decorative text that should ride along
/// with the playfield's update/draw loop.
/// </summary>
/// <remarks>
/// <see cref="Sprite2D.CanBeHit"/> defaults to <c>false</c> for text
/// sprites. <see cref="Sprite2D.Tint"/> sets the text color (including
/// alpha for fade-outs). <see cref="Sprite2D.Center"/> is the center
/// point of the rendered string; <see cref="Sprite2D.Scale"/> and
/// <see cref="Sprite2D.Rotation"/> are not currently honored — the
/// font's native pixel size is used as-is.
/// </remarks>
public class TextSprite2D : Sprite2D
{
    /// <summary>The font used to render <see cref="Text"/>.</summary>
    public Font? Font { get; set; }

    /// <summary>The string to render.</summary>
    public string Text { get; set; } = "";

    public TextSprite2D()
    {
        CanBeHit = false;
    }

    public override void Draw(Renderer2D renderer)
    {
        if (Font is null || Text.Length == 0)
            return;

        var size = Font.Measure(Text);
        var x = Center.X - size.X * 0.5f;
        var y = Center.Y - size.Y * 0.5f;
        Font.DrawText(renderer, Text, Tint, x, y);
    }
}
