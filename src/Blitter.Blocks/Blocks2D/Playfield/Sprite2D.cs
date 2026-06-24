using System.Numerics;


namespace Blitter.Blocks2D;

/// <summary>
/// A self-moving, collidable inhabitant of a <see cref="PlayField2D"/>.
/// Its collection of behaviors defines its logic: movement, collision response, and so on.
/// </summary>
public class Sprite2D : Entity, IDrawable2D
{
    public Sprite2D()
    {
    }

    private Transform2D? _transform = null;
    private Velocity2D? _velocity = null;
    private Appearance2D? _appearance = null;

    /// <summary>
    /// The sprite's position, rotation and scale in world space.
    /// </summary>
    public Transform2D Transform => _transform ?? this.GetOrAddTrait<Transform2D>();

    /// <summary>
    /// The sprite's velocity, expressed as speed and heading plus rotation speed.
    /// </summary>
    public Velocity2D Velocity => _velocity ?? this.GetOrAddTrait<Velocity2D>();

    /// <summary>
    /// How the sprite presents itself: its visual inputs, tint, and flip.
    /// </summary>
    public Appearance2D Appearance => _appearance ?? this.GetOrAddTrait<Appearance2D>();

    protected override void OnAttach(IEntity entity)
    {
        _transform = this.GetOrAddTrait<Transform2D>();
        _velocity = this.GetOrAddTrait<Velocity2D>();
        _appearance = this.GetOrAddTrait<Appearance2D>();
        if (!this.TryGetBehavior<ColliderShape2D>(out _))
            this.AddBehavior(new ColliderShape2D());
        _spawnedAt = TimeSpan.Zero;
        base.OnAttach(entity);
    }

    /// <summary>
    /// The sprite's image: a declarative <see cref="ImageSource"/> describing
    /// its look (a path, a texture, tiles, or named animation states). Always
    /// non-null; assigning <c>null</c> installs a fresh empty source (which
    /// draws nothing). Retains the authoring facts so the appearance is
    /// serializable; read <see cref="Visual"/> for the materialised result.
    /// </summary>
    public ImageSource Image
    {
        get => Appearance.Source;
        set => Appearance.Source = value ?? new();
    }

    /// <summary>
    /// The sprite's materialised visual, built and cached from <see cref="Image"/>.
    /// </summary>
    public Visual2D? Visual => Appearance.Source.GetComposedVisual();

    /// <summary>The position of the center of the sprite.</summary>
    public Vector2 Center { get => Transform.Position; set => Transform.Position = value; }

    /// <summary>The direction of movement.</summary>
    public float Heading { get => Velocity.Heading; set => Velocity.Heading = value; }

    /// <summary>The speed of the sprite in world units per second along the heading.</summary>
    public float Speed { get => Velocity.Speed; set => Velocity.Speed = value; }

    /// <summary>The current orientation in degrees.</summary>
    public float Rotation { get => Transform.Rotation; set => Transform.Rotation = value; }

    /// <summary>How many degrees the sprite rotates in a second.</summary>
    public float RotationSpeed { get => Velocity.RotationSpeed; set => Velocity.RotationSpeed = value; }

    /// <summary>The scale factor to apply to the visual.</summary>
    public float Scale { get => Transform.Scale; set => Transform.Scale = value; }

    /// <summary>
    /// Runtime mirror applied to the visual at draw time and to the
    /// hit shape when collisions are evaluated. Composes with any
    /// authoring flip on the visual's current animation frame.
    /// </summary>
    public FlipMode Flipped { get => Appearance.Flipped; set => Appearance.Flipped = value; }

    /// <summary>
    /// Tint color applied to the visual.
    /// </summary>
    public Color Tint { get => Appearance.Tint; set => Appearance.Tint = value; }

    /// <summary>
    /// The <see cref="PlayField2D"/> this sprite belongs to.
    /// </summary>
    public PlayField2D PlayField =>  
        this.Parent as PlayField2D 
            ?? throw new InvalidOperationException("Sprite is not attached to a PlayField. Access PlayField only while the sprite is a member of one.");

    /// <summary>
    /// The <see cref="Scene2D"/> this sprite's <see cref="PlayField"/>
    /// belongs to. Throws if the sprite is not in a playfield that is part
    /// of a running scene.
    /// </summary>
    public Scene2D Scene => PlayField.Scene;

    // Time sprite was added to playfield.
    internal TimeSpan _spawnedAt;

    /// <summary>
    /// How long this sprite has been a member of its current <see cref="PlayField"/>.
    /// </summary>
    public TimeSpan Age => 
        this.Parent is PlayField2D p 
            ? p.Elapsed - _spawnedAt 
            : TimeSpan.Zero;

    /// <summary>
    /// The sprite's world-space collision shape, provided by its
    /// <see cref="ColliderShape2D"/> behavior. Empty when the sprite has no
    /// collider (e.g. before it is attached to a playfield). Not overridable:
    /// to give a sprite custom geometry, set a <see cref="CollisionShape2D"/>
    /// trait (the collider reads it in preference to the visual's shape).
    /// </summary>
    public PosedHitShape2D HitShape =>
        this.TryGetBehavior<ColliderShape2D>(out var collider)
            ? collider.GetShape()
            : new(HitShape2D.None, Transform.Pose);

    /// <summary>
    /// Bounding circle of the sprite
    /// </summary>
    public BoundingCircle HitCircle => HitShape.BoundingCircle;

    /// <summary>Render the sprite at its current transform.</summary>
    public virtual void Draw(Renderer2D renderer)
    {
        this.Visual?.Draw(renderer, new Pose2D(Center, Rotation, Scale), this.Tint, this.Age, this.Flipped);
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
