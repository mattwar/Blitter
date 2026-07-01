using System.Numerics;


namespace Blitter.Blocks3D;

/// <summary>
/// A self-moving, collidable inhabitant of a <see cref="PlayField3D"/>.
/// Its collection of behaviors defines its logic: movement, collision
/// response, and so on. The 3D analog of <c>Blitter.Blocks2D.Sprite2D</c>.
/// </summary>
public class Sprite3D : Entity, IDrawable3D, IVisibility
{
    private Transform3D? _transform = null;
    private Velocity3D? _velocity = null;

    /// <summary>The visual to render.</summary>
    public Visual3D? Visual { get; set; }

    /// <summary>
    /// The sprite's position, orientation and scale in world space. The
    /// canonical pose behind <see cref="Position"/>, <see cref="Orientation"/>
    /// and <see cref="Scale"/>; read <see cref="Transform3D.Pose"/> to draw
    /// or hit-test.
    /// </summary>
    public Transform3D Transform => _transform ?? this.GetOrAddTrait<Transform3D>();

    /// <summary>
    /// The sprite's linear and angular velocity. Integrated by a motion
    /// behavior, not by the sprite itself.
    /// </summary>
    public Velocity3D Motion => _velocity ?? this.GetOrAddTrait<Velocity3D>();

    /// <summary>World-space position of the sprite's local origin.</summary>
    public Vector3 Position { get => Transform.Position; set => Transform.Position = value; }

    /// <summary>Orientation of the sprite's local axes in world space.</summary>
    public Quaternion Orientation { get => Transform.Orientation; set => Transform.Orientation = value; }

    /// <summary>Linear velocity in world units per second. Integrated by a motion behavior, not by the sprite itself.</summary>
    public Vector3 Velocity { get => Motion.Velocity; set => Motion.Velocity = value; }

    /// <summary>
    /// Angular velocity as an axis-times-radians-per-second vector. 
    /// The vector's direction is the rotation axis; its length is the angular speed.
    /// Integrated by a motion behavior, not by the sprite itself.
    /// </summary>
    public Vector3 AngularVelocity { get => Motion.AngularVelocity; set => Motion.AngularVelocity = value; }

    /// <summary>Uniform scale applied to the visual and hit shape.</summary>
    public float Scale { get => Transform.Scale; set => Transform.Scale = value; }

    /// <summary>Per-channel tint multiplied into the visual at draw time. Defaults to <see cref="Color.White"/>.</summary>
    public Color Tint { get; set; } = Color.White;

    /// <summary>
    /// Whether <see cref="Draw"/> renders the <see cref="Visual"/>.
    /// Set to <c>false</c> to keep the visual purely as a collision
    /// proxy (e.g. an invisible first-person body shape) — the hit
    /// shape is still derived from it.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// The host this sprite belongs to.
    /// </summary>
    public ISpriteHost3D? Host => this.Container as ISpriteHost3D;

    /// <summary>
    /// The sprite's world-space collision shape: the current
    /// <see cref="Visual"/>'s <see cref="Visual3D.HitShape"/> combined
    /// with this sprite's <see cref="Position"/>, <see cref="Orientation"/>,
    /// and <see cref="Scale"/>. Override to substitute a different shape
    /// (still posed by the sprite).
    /// </summary>
    public virtual PosedHitShape3D HitShape =>
        new(Visual?.HitShape ?? HitShape3D.None, Transform.Pose);

    /// <summary>
    /// Bounding sphere of the sprite for collision purposes; equivalent
    /// to <see cref="HitShape"/>'s <see cref="PosedHitShape3D.BoundingSphere"/>.
    /// </summary>
    public BoundingSphere HitSphere => HitShape.BoundingSphere;

    public Sprite3D()
    {
    }

    protected override void OnAttach(IEntity entity)
    {
        _transform = this.GetOrAddTrait<Transform3D>();
        _velocity = this.GetOrAddTrait<Velocity3D>();
        base.OnAttach(entity);
    }

    /// <summary>Render the sprite at its current transform.</summary>
    public virtual void Draw(Renderer3D renderer)
    {
        if (!this.Visible)
            return;
        this.Visual?.Draw(renderer, Transform.Pose, this.Tint, TimeSpan.Zero);
    }
}
