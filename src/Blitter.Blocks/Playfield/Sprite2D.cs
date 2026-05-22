using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks;

/// <summary>
/// A self-moving, collidable inhabitant of a <see cref="PlayField2D"/>.
/// Its collection of behaviors defines its logic: movement, collision response, and so on.
/// </summary>
public class Sprite2D : IUpdatable<UpdateContext2D>, IDrawable2D
{
    /// <summary>The image to render.</summary>
    public SpriteImage2D? Image { get; set; }

    /// <summary>The position of the center of the sprite.</summary>
    public Vector2 Center { get; set; }

    /// <summary>The direction of movement.</summary>
    public float Heading { get; set; }

    /// <summary>The speed of the sprite in world units per second along the heading.</summary>
    public float Speed { get; set; }

    /// <summary>The current orientation in degrees.</summary>
    public float Rotation { get; set; }

    /// <summary>How many degrees the sprite rotates in a second.</summary>
    public float RotationSpeed { get; set; }

    /// <summary>The scale factor to apply to the image.</summary>
    public float Scale { get; set; } = 1f;

    /// <summary>The flip mode to apply when rendering the image.</summary>
    public FlipMode Flipped = FlipMode.None;

    /// <summary>Per-channel tint multiplied into the image at draw time.
    /// Defaults to <see cref="Color.White"/> (no change).</summary>
    public Color Tint { get; set; } = Color.White;

    /// <summary>
    /// Whether this sprite participates in the playfield's hit-detection pass.
    /// Set to <c>false</c> for purely decorative sprites (score popups,
    /// particles, debris) that should move and render but never trigger
    /// <see cref="OnHitSprite"/> or <see cref="OnHitBarrier"/>.
    /// </summary>
    public bool CanBeHit { get; set; } = true;

    /// <summary>
    /// Behaviors attached to this sprite. Run in list order each tick.
    /// </summary>
    public List<SpriteBehavior2D> Behaviors { get; } = new();

    /// <summary>
    /// The sprite is active and not about to be culled.
    /// </summary>
    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// The <see cref="PlayField2D"/> this sprite belongs to.
    /// </summary>
    public PlayField2D PlayField =>  
        _playField ?? throw new InvalidOperationException("Sprite is not attached to a PlayField. Access PlayField only while the sprite is a member of one.");

    // PlayField backing field.
    internal PlayField2D? _playField;

    // Time sprite was added to playfield.
    internal TimeSpan _spawnedAt;

    /// <summary>
    /// How long this sprite has been a member of its current <see cref="PlayField"/>.
    /// </summary>
    public TimeSpan Age => 
        _playField is { } p 
            ? p.Elapsed - _spawnedAt 
            : TimeSpan.Zero;

    /// <summary>
    /// The sprite's world-space collision shape: the current
    /// <see cref="Image"/>'s <see cref="SpriteImage2D.HitShape"/>
    /// combined with this sprite's <see cref="Center"/>,
    /// <see cref="Rotation"/>, <see cref="Scale"/>, and
    /// <see cref="Flipped"/>. Override to substitute a different
    /// shape (still posed by the sprite).
    /// </summary>
    public virtual PosedHitShape2D HitShape =>
        new(Image?.HitShape ?? HitShape2D.None, new Pose2D(Center, Rotation, Scale, Flipped));

    /// <summary>
    /// Broad-phase bounding circle of the sprite for collision
    /// purposes; equivalent to <see cref="HitShape"/>'s
    /// <see cref="PosedHitShape2D.BroadCircle"/>.
    /// </summary>
    public BoundingCircle HitCircle => HitShape.BroadCircle;

    public Sprite2D()
    {
    }

    /// <summary>Apply every enabled behavior in order.</summary>
    public virtual void Update(in UpdateContext2D context)
    {
        foreach (var behavior in this.Behaviors)
        {
            if (behavior.Enabled)
                behavior.Apply(this, in context);
        }
    }

    /// <summary>
    /// Called by the owning <see cref="PlayField2D"/> when this
    /// sprite's <see cref="HitCircle"/> intersects another sprite's.
    /// Forwards to each enabled behavior.
    /// </summary>
    public virtual void OnHitSprite(Sprite2D other, in UpdateContext2D context)
    {
        foreach (var behavior in this.Behaviors)
        {
            if (behavior.Enabled)
                behavior.OnHitSprite(this, other, in context);
        }
    }

    /// <summary>
    /// Called by the owning <see cref="PlayField2D"/> when this
    /// sprite's <see cref="HitCircle"/> overlaps a
    /// <see cref="Barrier2D"/>. Forwards to each enabled behavior.
    /// </summary>
    public virtual void OnHitBarrier(Barrier2D barrier, in UpdateContext2D context)
    {
        foreach (var behavior in this.Behaviors)
        {
            if (behavior.Enabled)
                behavior.OnHitBarrier(this, barrier, in context);
        }
    }

    /// <summary>Render the sprite at its current transform.</summary>
    public virtual void Draw(Renderer2D renderer)
    {
        this.Image?.Draw(renderer, new Pose2D(Center, Rotation, Scale, Flipped), this.Tint, this.Age);
    }

    /// <summary>
    /// Get the velocity vector from speed and heading (degrees).
    /// </summary>
    public static Vector2 GetVelocity(float speed, float heading)
    {
        double headingRads = (heading - 90f) * (Math.PI / 180.0);
        var velocityX = speed * (float)Math.Cos(headingRads);
        if (MathF.Abs(velocityX) < 0.0001f)
            velocityX = 0f;
        var velocityY = speed * (float)Math.Sin(headingRads);
        if (MathF.Abs(velocityY) < 0.0001f)
            velocityY = 0f;
        return new Vector2(velocityX, velocityY);
    }

    /// <summary>
    /// Gets speed and heading (degrees) from a velocity vector.
    /// </summary>
    public static (float speed, float heading) GetSpeedAndHeading(Vector2 velocity)
    {
        var speed = velocity.Length();
        var heading = (float)(Math.Atan2(velocity.Y, velocity.X) * (180.0 / Math.PI) + 90f);
        if (heading < 0)
            heading += 360f;
        else if (heading >= 360f)
            heading -= 360f;
        return (speed, heading);
    }

}
