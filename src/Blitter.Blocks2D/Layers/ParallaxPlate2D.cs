using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks2D;

/// <summary>
/// One plate (back-to-front slice) of a <see cref="ParallaxBackground2D"/>:
/// an image plus the parallax factor that controls how fast it scrolls
/// relative to the camera. The <see cref="Image"/> is an <see cref="ImageSource"/>,
/// so a bare file-path string converts implicitly
/// (<c>{ "mountains.png", 0.3f }</c>). Per-plate <see cref="BottomY"/>,
/// <see cref="OffsetX"/>, <see cref="RepeatX"/>, and <see cref="Tint"/> override
/// the owning layer's shared defaults.
/// </summary>
public sealed class ParallaxPlate2D
{
    private ImageSource _image = new();
    private Texture2D? _texture;

    /// <summary>The image drawn for this plate.</summary>
    public ImageSource Image
    {
        get => _image;
        set
        {
            _image = value ?? new();
            _texture = null;
        }
    }

    /// <summary>
    /// Per-axis parallax factor applied to the camera when this plate draws.
    /// <c>(1, 1)</c> moves with the foreground (the default), <c>(0, 0)</c> is
    /// locked to the screen, values in between drift (distant background), and
    /// values &gt; 1 move faster than the foreground.
    /// </summary>
    public Vector2 Parallax { get; set; } = Vector2.One;

    /// <summary>
    /// World-space Y at which the bottom edge of the image sits. When unset the
    /// owning <see cref="ParallaxBackground2D.BottomY"/> is used. The image's
    /// top edge ends up at <c>BottomY - Image.Height</c>.
    /// </summary>
    public float? BottomY { get; set; }

    /// <summary>
    /// When true (the default) the image is tiled horizontally to cover the
    /// viewport; when false a single copy is drawn, centred horizontally on the
    /// camera (with <see cref="OffsetX"/> as a nudge).
    /// </summary>
    public bool RepeatX { get; set; } = true;

    /// <summary>Horizontal offset added to the tile pattern (or the centred single copy).</summary>
    public float OffsetX { get; set; }

    /// <summary>Tint multiplied into the image at draw time.</summary>
    public Color Tint { get; set; } = Color.White;

    // The plate's materialised texture, built once from Image and reused.
    internal Texture2D Texture => _texture ??= _image.ToTexture();
}
