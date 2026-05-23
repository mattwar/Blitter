using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// A <see cref="Visual2D"/> backed by a single <see cref="Texture2D"/>.
/// The boundary is the texture's opaque-pixel bounding circle, computed
/// once on first access.
/// </summary>
public sealed class TextureVisual2D : Visual2D
{
    private readonly Texture2D _texture;
    private readonly HitShapeCache _hitShapeCache;
    private BoundingCircle? _boundary;

    public TextureVisual2D(Texture2D texture, HitShapeCache? hitShapeCache = null)
    {
        ArgumentNullException.ThrowIfNull(texture);
        _texture = texture;
        _hitShapeCache = hitShapeCache ?? HitShapeCache.Default;
    }

    public Texture2D Texture => _texture;

    public override BoundingCircle Boundary =>
        _boundary ??= ComputeBoundary();

    private BoundingCircle ComputeBoundary()
    {
        // Pixel-accurate boundary requires CPU pixel access (ReadableTexture2D).
        // For other Texture2D backings, fall back to the circle that
        // circumscribes the full image rect.
        var size = _texture.Size;
        if (_texture is ReadableTexture2D readable)
            return ToVisualLocal(readable.ComputeOpaqueCircle(), size);
        var half = new Vector2(size.Width / 2f, size.Height / 2f);
        return new BoundingCircle(Vector2.Zero, half.Length());
    }

    public override void Draw(Renderer2D renderer, in Pose2D pose, Color tint, TimeSpan elapsed)
    {
        var size = _texture.Size;
        var scaledWidth = size.Width * pose.Scale;
        var scaledHeight = size.Height * pose.Scale;
        var source = new Rect(0, 0, size.Width, size.Height);
        var dest = new Rect(pose.Position.X - scaledWidth / 2f, pose.Position.Y - scaledHeight / 2f, scaledWidth, scaledHeight);
        bool tinted = tint != Color.White;

        if (pose.Rotation != 0f)
        {
            var rotationCenter = new Vector2(scaledWidth / 2f, scaledHeight / 2f);
            if (tinted)
                renderer.DrawImageRotated(_texture, source, dest, pose.Rotation, rotationCenter, FlipMode.None, tint);
            else
                renderer.DrawImageRotated(_texture, source, dest, pose.Rotation, rotationCenter);
        }
        else
        {
            if (tinted)
                renderer.DrawImage(_texture, source, dest, tint);
            else
                renderer.DrawImage(_texture, source, dest);
        }
    }

    // Convert a bounding circle from texture-local (top-left origin) to visual-local (center origin) coordinates.
    private static BoundingCircle ToVisualLocal(BoundingCircle c, (int Width, int Height) size) =>
        c.IsEmpty ? c
        : new BoundingCircle(c.Center - new Vector2(size.Width / 2f, size.Height / 2f), c.Radius);

    /// <inheritdoc/>
    public override HitShape2D HitShape =>
        _hitShapeCache.GetOrCreateHitShape(_texture);
}
