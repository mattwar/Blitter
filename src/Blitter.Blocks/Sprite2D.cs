using System.Numerics;
using Blitter.Bits;

namespace Blitter.Blocks;

/// <summary>
/// A prop that moves itself.
/// </summary>
public class Sprite2D : Prop2D
{
    /// <summary>
    /// The image to render.
    /// </summary>
    public Texture2D? Image { get; set; }

    /// <summary>
    /// The X position of the center of the sprite.
    /// </summary>    
    public float CenterX { get; set; }

    /// <summary>
    /// The Y position of the center of the sprite.
    /// </summary>
    public float CenterY { get; set; }

    /// <summary>
    /// The direction of movement.
    /// </summary>
    public float Heading { get; set; }

    /// <summary>
    /// The speed of the sprite in pixels/units per second along the heading.
    /// </summary>
    public float Speed { get; set; }

    /// <summary>
    /// The current orientation in degrees.
    /// </summary>
    public float Rotation { get; set; }

    /// <summary>
    /// How many degrees the sprite rotates in a second.
    /// </summary>
    public float RotationSpeed { get; set; }

    /// <summary>
    /// The scale factor to apply to the image.
    /// </summary>
    public float Scale { get; set; } = 1f;

    /// <summary>
    /// The flip mode to apply when rendering the image.
    /// </summary>
    public FlipMode Flipped = FlipMode.None;

    /// <summary>
    /// Radius around <see cref="CenterX"/>/<see cref="CenterY"/> used
    /// for collision detection. Zero (the default) means the sprite is
    /// not collidable.
    /// </summary>
    public float HitRadius { get; set; }

    /// <inheritdoc/>
    public override BoundingCircle? HitCircle =>
        HitRadius > 0f
            ? new BoundingCircle(new Vector2(CenterX, CenterY), HitRadius)
            : null;

    /// <summary>
    /// Behaviors attached to this sprite. Run in list order each tick.
    /// Typical behaviors mutate the sprite's position, rotation, or
    /// velocity (see <see cref="Motion2D"/>, <see cref="BounceInBounds2D"/>).
    /// </summary>
    public List<SpriteBehavior2D> Behaviors { get; } = new();

    public Sprite2D()
    {
    }

    public Sprite2D(Texture2D image, float centerX, float centerY, float scale = 1f)
    {
        this.Image = image;
        this.CenterX = centerX;
        this.CenterY = centerY;
        this.Scale = scale;
    }

    public override bool Update(in UpdateContext2D context)
    {
        foreach (var behavior in this.Behaviors)
        {
            if (behavior.Enabled)
                behavior.Update(this, in context);
        }

        return true;
    }

    /// <inheritdoc/>
    public override void OnCollision(Prop2D other, in UpdateContext2D context)
    {
        foreach (var behavior in this.Behaviors)
        {
            if (behavior.Enabled)
                behavior.OnCollision(this, other, in context);
        }
    }

    /// <summary>
    /// Get velocity components from speed and heading (degrees).
    /// </summary>
    public static (float velocityX, float velocityY) GetVelocity(float speed, float heading)
    {
        double headingRads = (heading - 90f) * (Math.PI / 180.0);
        var velocityX = speed * (float)Math.Cos(headingRads);
        if (MathF.Abs(velocityX) < 0.0001f)
            velocityX = 0f;
        var velocityY = speed * (float)Math.Sin(headingRads);
        if (MathF.Abs(velocityY) < 0.0001f)
            velocityY = 0f;
        return (velocityX, velocityY);
    }

    /// <summary>
    /// Gets speed and heading (degrees) from velocity components.
    /// </summary>
    public static (float speed, float heading) GetSpeedAndHeading(float velocityX, float velocityY)
    {
        var speed = (float)Math.Sqrt(velocityX * velocityX + velocityY * velocityY);
        var heading = (float)(Math.Atan2(velocityY, velocityX) * (180.0 / Math.PI) + 90f);
        if (heading < 0)
            heading += 360f;
        else if (heading >= 360f)
            heading -= 360f;
        return (speed, heading);
    }

    public void ChangeVelocity(Func<float, float, (float vx, float vy)> fn)
    {
        var (vx, vy) = GetVelocity(this.Speed, this.Heading);
        (vx, vy) = fn(vx, vy);
        (this.Speed, this.Heading) = GetSpeedAndHeading(vx, vy);
    }

    public override void Draw(Renderer2D renderer)
    {
        if (this.Image is { } image)
        {
            var size = image.Size;
            var scaledWidth = size.Width * this.Scale;
            var scaledHeight = size.Height * this.Scale;
            var x = this.CenterX - scaledWidth / 2;
            var y = this.CenterY - scaledHeight / 2;
            var source = new Rect(0, 0, size.Width, size.Height);
            var dest = new Rect(x, y, scaledWidth, scaledHeight);
            var center = new Vector2(scaledWidth / 2f, scaledHeight / 2f);

            if (this.Rotation != 0f || this.Flipped != FlipMode.None)
            {
                renderer.DrawImageRotated(image, source, dest, this.Rotation, center, this.Flipped);
            }
            else
            {
                renderer.DrawImage(image, source, dest);
            }
        }
    }
}
