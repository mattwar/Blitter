using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// A self-moving, collidable inhabitant of a <see cref="PlayField3D"/>.
/// Its collection of behaviors defines its logic: movement, collision
/// response, and so on. The 3D analog of <c>Blitter.Blocks2D.Sprite2D</c>.
/// </summary>
public class Sprite3D : IUpdatable<UpdateContext3D>, IDrawable3D
{
    /// <summary>The visual to render.</summary>
    public Visual3D? Visual { get; set; }

    /// <summary>World-space position of the sprite's local origin.</summary>
    public Vector3 Position { get; set; }

    /// <summary>Orientation of the sprite's local axes in world space.</summary>
    public Quaternion Orientation { get; set; } = Quaternion.Identity;

    /// <summary>Linear velocity in world units per second. Integrated by a motion behavior, not by the sprite itself.</summary>
    public Vector3 Velocity { get; set; }

    /// <summary>
    /// Angular velocity as an axis-times-radians-per-second vector. 
    /// The vector's direction is the rotation axis; its length is the angular speed.
    /// Integrated by a motion behavior, not by the sprite itself.
    /// </summary>
    public Vector3 AngularVelocity { get; set; }

    /// <summary>Uniform scale applied to the visual and hit shape.</summary>
    public float Scale { get; set; } = 1f;

    /// <summary>Per-channel tint multiplied into the visual at draw time. Defaults to <see cref="Color.White"/>.</summary>
    public Color Tint { get; set; } = Color.White;

    /// <summary>
    /// Whether this sprite participates in the playfield's hit-detection pass.
    /// Set to <c>false</c> for purely decorative sprites that should move
    /// and render but never trigger collision callbacks.
    /// </summary>
    public bool CanBeHit { get; set; } = true;

    /// <summary>Behaviors attached to this sprite. Run in list order each frame.</summary>
    public List<SpriteBehavior3D> Behaviors { get; } = new();

    /// <summary>The sprite is active and not about to be culled.</summary>
    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// The host this sprite belongs to.
    /// </summary>
    public ISpriteHost3D? Host 
    {
        get; 

        set
        {
            if (value != field)
            {
                if (field is {} oldHost)
                {
                    oldHost.RemoveSprite(this);             
                }

                field = value;

                if (value is {} newHost)
                {
                    newHost.AddSprite(this);
                    _spawnedAt = newHost.Elapsed;
                }               
            }
        }
    }
        
    // Time sprite was added to its current host.
    private TimeSpan _spawnedAt;

    /// <summary>How long this sprite has been a member of its current <see cref="Host"/>.</summary>
    public TimeSpan Age =>
        this.Host is { } p
            ? p.Elapsed - _spawnedAt
            : TimeSpan.Zero;

    /// <summary>
    /// The sprite's world-space collision shape: the current
    /// <see cref="Visual"/>'s <see cref="Visual3D.HitShape"/> combined
    /// with this sprite's <see cref="Position"/>, <see cref="Orientation"/>,
    /// and <see cref="Scale"/>. Override to substitute a different shape
    /// (still posed by the sprite).
    /// </summary>
    public virtual PosedHitShape3D HitShape =>
        new(Visual?.HitShape ?? HitShape3D.None, new Pose3D(Position, Orientation, Scale));

    /// <summary>
    /// Bounding sphere of the sprite for collision purposes; equivalent
    /// to <see cref="HitShape"/>'s <see cref="PosedHitShape3D.BoundingSphere"/>.
    /// </summary>
    public BoundingSphere HitSphere => HitShape.BoundingSphere;

    public Sprite3D()
    {
    }

    /// <summary>Apply every enabled behavior in order.</summary>
    public virtual void Update(in UpdateContext3D context)
    {
        foreach (var behavior in this.Behaviors)
        {
            if (behavior.Enabled)
                behavior.Apply(this, in context);
        }
    }

    /// <summary>
    /// Called by the owning <see cref="PlayField3D"/> when this sprite's
    /// <see cref="HitShape"/> intersects another sprite's. Forwards to
    /// each enabled behavior.
    /// </summary>
    public virtual void OnHitSprite(Sprite3D other, in UpdateContext3D context)
    {
        foreach (var behavior in this.Behaviors)
        {
            if (behavior.Enabled)
                behavior.OnHitSprite(this, other, in context);
        }
    }

    /// <summary>
    /// Called by the owning <see cref="PlayField3D"/> when this sprite's
    /// <see cref="HitSphere"/> overlaps a <see cref="Barrier3D"/>.
    /// Forwards to each enabled behavior.
    /// </summary>
    public virtual void OnHitBarrier(Barrier3D barrier, in UpdateContext3D context)
    {
        foreach (var behavior in this.Behaviors)
        {
            if (behavior.Enabled)
                behavior.OnHitBarrier(this, barrier, in context);
        }
    }

    /// <summary>Render the sprite at its current transform.</summary>
    public virtual void Draw(Renderer3D renderer)
    {
        this.Visual?.Draw(renderer, new Pose3D(Position, Orientation, Scale), this.Tint, this.Age);
    }
}
