namespace Blitter.Blocks;

/// <summary>
/// A <see cref="Layer2D"/> that draws a single image tiled
/// horizontally to cover the viewport. Pair with
/// <see cref="Layer2D.ParallaxFactor"/> to build a parallax
/// background stack: each layer at a different factor scrolls at a
/// different speed relative to the camera.
/// </summary>
public sealed class RepeatingImageLayer2D : Layer2D
{
    private readonly Texture2D _image;

    /// <summary>Creates a layer that tiles <paramref name="image"/> horizontally.</summary>
    public RepeatingImageLayer2D(Texture2D image)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
    }

    /// <summary>The image drawn by this layer.</summary>
    public Texture2D Image => _image;

    /// <summary>
    /// World-space Y at which the bottom edge of the image sits.
    /// The image's top edge ends up at <c>BottomY - Image.Height</c>.
    /// </summary>
    public float BottomY { get; set; }

    /// <summary>
    /// Horizontal offset added to the tile pattern. Useful for nudging
    /// a layer slightly out of alignment with its neighbors.
    /// </summary>
    public float OffsetX { get; set; }

    /// <summary>
    /// When true (the default) the image is tiled across the viewport;
    /// when false a single copy is drawn at <see cref="OffsetX"/>.
    /// </summary>
    public bool RepeatX { get; set; } = true;

    /// <summary>Tint multiplied into the image at draw time.</summary>
    public Color Tint { get; set; } = Color.White;

    /// <inheritdoc/>
    public override void Update(in UpdateContext2D context) { }

    /// <inheritdoc/>
    protected override void DrawContent(Renderer2D renderer)
    {
        float tileW = _image.Width;
        float tileH = _image.Height;
        float topY = BottomY - tileH;

        // Figure out which world-X range the viewport currently shows
        // so we know how many tile copies to emit. Layer2D has already
        // swapped in a parallax-adjusted camera by the time we draw,
        // so reading renderer.Camera here is correct per layer.
        var cam = renderer.Camera;
        if (!RepeatX || tileW <= 0f || cam is null)
        {
            DrawAt(renderer, OffsetX, topY, tileW, tileH);
            return;
        }

        var (vw, _) = renderer.LogicalSize;
        if (vw <= 0)
            (vw, _) = renderer.OutputSize;
        float zoom = cam.Zoom > 0f ? cam.Zoom : 1f;
        float halfViewW = (vw / zoom) * 0.5f;
        float viewLeft = cam.Position.X - halfViewW;
        float viewRight = cam.Position.X + halfViewW;

        int first = (int)MathF.Floor((viewLeft - OffsetX) / tileW);
        int last  = (int)MathF.Ceiling((viewRight - OffsetX) / tileW);
        for (int i = first; i <= last; i++)
        {
            DrawAt(renderer, OffsetX + i * tileW, topY, tileW, tileH);
        }
    }

    private void DrawAt(Renderer2D renderer, float x, float y, float w, float h)
    {
        var dst = new Rect(x, y, w, h);
        if (Tint == Color.White)
            renderer.DrawImage(_image, dst);
        else
            renderer.DrawImage(_image, new Rect(0, 0, _image.Width, _image.Height), dst, Tint);
    }
}
