namespace Blitter;

/// <summary>
/// A <see cref="Renderer2D"/> that draws into a <see cref="Bitmap"/>
/// </summary>
internal sealed class BitmapRenderer2D : TextureRenderer2D
{
    private readonly Bitmap _image;

    private BitmapRenderer2D(Bitmap image, nint rendererId)
        : base(rendererId)
    {
        _image = image;
    }

    /// <summary>
    /// Creates a software renderer that draws into <paramref name="image"/>.
    /// </summary>
    public static BitmapRenderer2D Create(Bitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);
        image.ThrowIfDisposed();

        _ = Application.Current;
        SDL.InitSubSystem(SDL.InitFlags.Video);

        var rendererId = SDL.CreateSoftwareRenderer(image._imageId);
        if (rendererId == 0)
            throw new InvalidOperationException(
                $"Failed to create software renderer for image: {SDL.GetError()}");

        return new BitmapRenderer2D(image, rendererId);
    }

    /// <summary>The <see cref="Blitter.Bitmap"/> this renderer draws into.</summary>
    public Bitmap Bitmap => _image;

    protected override void OnDisposed()
    {
        // Pixels were written through SDL's renderer rather than the
        // version-tracked SetPixel path, so any cached GPU upload of this
        // image needs to re-stage on next use.
        if (!_image.IsDisposed)
            _image.Invalidate();
    }
}

