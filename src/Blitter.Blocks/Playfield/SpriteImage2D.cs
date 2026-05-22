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
    /// Name of the implicit single state for images that have no
    /// animation or sequence concept.
    /// </summary>
    public const string DefaultState = "default";

    private static readonly IReadOnlyList<string> _defaultStates = [DefaultState];

    /// <summary>
    /// Currently selected animation, pose, or variant name. Setting
    /// it switches which frames the image draws and which
    /// <see cref="Boundary"/> / <see cref="HitShape"/> it reports.
    /// Single-state images keep this at <see cref="DefaultState"/>.
    /// </summary>
    public virtual string State { get; set; } = DefaultState;

    /// <summary>
    /// All state names this image can switch between, in declaration
    /// order. Single-state images expose just <see cref="DefaultState"/>.
    /// </summary>
    public virtual IReadOnlyList<string> States => _defaultStates;

    /// <summary>
    /// Bounding circle in sprite-local coordinates (origin at sprite
    /// center, unscaled). The sprite applies its own
    /// <see cref="Sprite2D.Center"/> and <see cref="Sprite2D.Scale"/>
    /// when computing world-space collision.
    /// </summary>
    public abstract BoundingCircle Boundary { get; }

    /// <summary>
    /// Image-local collision shape. Defaults to a single circle
    /// derived from <see cref="Boundary"/>; assign a hand-rolled
    /// shape (e.g. a capsule along the visible body) for tighter
    /// geometry. The same instance can be shared across sprites and
    /// across animation frames.
    /// </summary>
    public HitShape2D HitShape
    {
        get => _hitShape ??= DeriveHitShape();
        set => _hitShape = value;
    }
    private HitShape2D? _hitShape;

    /// <summary>
    /// Produces the default <see cref="HitShape2D"/> when none has
    /// been explicitly set. The base implementation returns a circle
    /// from <see cref="Boundary"/>; subclasses with pixel access can
    /// fit a tighter shape.
    /// </summary>
    protected virtual HitShape2D DeriveHitShape() =>
        new CircleHitShape2D(Boundary.Center, Boundary.Radius);

    /// <summary>Draw this image at the given world <paramref name="pose"/>,
    /// multiplied by <paramref name="tint"/> (per-channel). 
    /// Pass <see cref="Color.White"/> for untinted output.</summary>
    public abstract void Draw(Renderer2D renderer, in Pose2D pose, Color tint);

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

    public override void Draw(Renderer2D renderer, in Pose2D pose, Color tint)
    {
        var size = _texture.Size;
        var scaledWidth = size.Width * pose.Scale;
        var scaledHeight = size.Height * pose.Scale;
        var source = new Rect(0, 0, size.Width, size.Height);
        var dest = new Rect(pose.Position.X - scaledWidth / 2f, pose.Position.Y - scaledHeight / 2f, scaledWidth, scaledHeight);
        bool tinted = tint != Color.White;

        if (pose.Rotation != 0f || pose.Flipped != FlipMode.None)
        {
            var rotationCenter = new Vector2(scaledWidth / 2f, scaledHeight / 2f);
            if (tinted)
                renderer.DrawImageRotated(_texture, source, dest, pose.Rotation, rotationCenter, pose.Flipped, tint);
            else
                renderer.DrawImageRotated(_texture, source, dest, pose.Rotation, rotationCenter, pose.Flipped);
        }
        else
        {
            if (tinted)
                renderer.DrawImage(_texture, source, dest, tint);
            else
                renderer.DrawImage(_texture, source, dest);
        }
    }

    // Convert a bounding circle from texture-local (top-left origin) to sprite-local (center origin) coordinates.
    private static BoundingCircle ToSpriteLocal(BoundingCircle c, (int Width, int Height) size) =>
        c.IsEmpty ? c
        : new BoundingCircle(c.Center - new Vector2(size.Width / 2f, size.Height / 2f), c.Radius);

    protected override HitShape2D DeriveHitShape()
    {
        if (_texture is not Bitmap bmp) 
            return base.DeriveHitShape();
        var opaqueShape = bmp.ComputeOpaqueHitShape2D();
        // Compute returned pixel-space coords (top-left origin); shift to sprite-local (image-centered) coords.
        var size = _texture.Size;
        return opaqueShape.Translate(new Vector2(-size.Width / 2f, -size.Height / 2f));
    }
}
