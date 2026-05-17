using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks;

/// <summary>
/// The visual+bounds for a <see cref="Sprite2D"/>. Implementations
/// know how to draw themselves at a given transform and report a
/// sprite-local bounding circle the sprite uses for collision.
/// </summary>
public abstract class SpriteImage2D
{
    /// <summary>
    /// Bounding circle in sprite-local coordinates (origin at sprite
    /// center, unscaled). The sprite applies its own
    /// <see cref="Sprite2D.Center"/> and <see cref="Sprite2D.Scale"/>
    /// when computing world-space collision.
    /// </summary>
    public abstract BoundingCircle Boundary { get; }

    /// <summary>Draw this image at the given world transform.</summary>
    public abstract void Draw(
        Renderer2D renderer,
        Vector2 center,
        float rotation,
        float scale,
        FlipMode flipped);

    /// <summary>
    /// Implicit wrap of a <see cref="Texture2D"/> in a
    /// <see cref="TextureSpriteImage2D"/> so callers can assign a
    /// texture directly to <see cref="Sprite2D.Image"/>.
    /// </summary>
    public static implicit operator SpriteImage2D(Texture2D texture) =>
        new TextureSpriteImage2D(texture);
}

/// <summary>
/// A <see cref="SpriteImage2D"/> backed by a single <see cref="Texture2D"/>.
/// The boundary is the texture's opaque-pixel bounding circle, computed
/// once on first access.
/// </summary>
public sealed class TextureSpriteImage2D : SpriteImage2D
{
    private readonly Texture2D _texture;
    private BoundingCircle? _boundary;

    public TextureSpriteImage2D(Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        _texture = texture;
    }

    public Texture2D Texture => _texture;

    public override BoundingCircle Boundary =>
        _boundary ??= ComputeBoundary();

    private BoundingCircle ComputeBoundary()
    {
        // Pixel-accurate boundary requires CPU pixel access (Bitmap).
        // For other Texture2D backings, fall back to the circle that
        // circumscribes the full image rect.
        var size = _texture.Size;
        if (_texture is Bitmap bmp)
            return ToSpriteLocal(bmp.ComputeOpaqueCircle(), size);
        var half = new Vector2(size.Width / 2f, size.Height / 2f);
        return new BoundingCircle(Vector2.Zero, half.Length());
    }

    public override void Draw(Renderer2D renderer, Vector2 center, float rotation, float scale, FlipMode flipped)
    {
        var size = _texture.Size;
        var scaledWidth = size.Width * scale;
        var scaledHeight = size.Height * scale;
        var source = new Rect(0, 0, size.Width, size.Height);
        var dest = new Rect(center.X - scaledWidth / 2f, center.Y - scaledHeight / 2f, scaledWidth, scaledHeight);

        if (rotation != 0f || flipped != FlipMode.None)
        {
            var rotationCenter = new Vector2(scaledWidth / 2f, scaledHeight / 2f);
            renderer.DrawImageRotated(_texture, source, dest, rotation, rotationCenter, flipped);
        }
        else
        {
            renderer.DrawImage(_texture, source, dest);
        }
    }

    // ComputeOpaqueCircle returns coords in image-pixel space (origin
    // at top-left). Sprite draws the image centered, so translate the
    // circle so its origin matches the sprite center.
    private static BoundingCircle ToSpriteLocal(BoundingCircle c, (int Width, int Height) size) =>
        c.IsEmpty
            ? c
            : new BoundingCircle(c.Center - new Vector2(size.Width / 2f, size.Height / 2f), c.Radius);
}
