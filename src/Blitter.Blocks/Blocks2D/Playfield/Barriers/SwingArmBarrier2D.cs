using System.Numerics;


namespace Blitter.Blocks2D;

/// <summary>
/// A pivoted capsule barrier that swings between a rest angle and an
/// active angle when <see cref="Pressed"/> is toggled. Useful for
/// pinball flippers, swinging gates and trapdoors, mechanical arms,
/// and any actuated rotating obstacle.
/// </summary>
/// <remarks>
/// Angles are measured in degrees from the +X axis in screen space
/// (Y is down), so 0° points right, 90° points down, -90° points up.
/// Set <see cref="Pressed"/> from input each frame; the barrier
/// interpolates toward the corresponding target angle at
/// <see cref="SnapDegPerSec"/>. <see cref="SurfaceBounce2D"/>
/// reads the angular velocity to add a surface kick to the ball.
/// </remarks>
public class SwingArmBarrier2D : Barrier2D
{
    public Vector2 Pivot { get; init; }
    public float Length { get; init; }

    /// <summary>Capsule fatness — combined with the sprite's hit radius for collision.</summary>
    public float Radius { get; init; } = 8f;

    /// <summary>Angle (degrees) when <see cref="Pressed"/> is false.</summary>
    public float RestAngleDeg { get; init; }

    /// <summary>Angle (degrees) when <see cref="Pressed"/> is true.</summary>
    public float ActiveAngleDeg { get; init; }

    /// <summary>Angular slew rate while moving toward the target angle.</summary>
    public float SnapDegPerSec { get; set; } = 1800f;

    /// <summary>Set by input each frame. The flipper moves toward <see cref="ActiveAngleDeg"/> while true.</summary>
    public bool Pressed { get; set; }

    // Tracks the Pressed value from the previous Update so we can fire
    // OnPressed/OnReleased exactly once on each edge transition.
    private bool _wasPressed;

    /// <summary>Current angle in degrees. Initialized to <see cref="RestAngleDeg"/> on first update.</summary>
    public float CurrentAngleDeg { get; private set; } = float.NaN;

    /// <summary>Signed angular velocity in radians/second from the last update.</summary>
    public float AngularVelRadPerSec { get; private set; }

    /// <summary>Tip endpoint of the segment in world space.</summary>
    public Vector2 Tip
    {
        get
        {
            var rad = CurrentAngleDeg * MathF.PI / 180f;
            return Pivot + new Vector2(MathF.Cos(rad), MathF.Sin(rad)) * Length;
        }
    }

    /// <inheritdoc/>
    protected override void OnAttach(IEntity entity)
    {
        base.OnAttach(entity);
        // Collision geometry is a static capsule along the local +X axis from
        // the pivot; the Transform's rotation (driven each Update) swings it.
        this.GetOrAddTrait<CollisionShape2D>().Shape =
            new CapsuleHitShape2D(Vector2.Zero, new Vector2(Length, 0f), Radius);
        Transform.Position = Pivot;
        Transform.Rotation = float.IsNaN(CurrentAngleDeg) ? RestAngleDeg : CurrentAngleDeg;
        // Expose the swing's surface velocity to SurfaceBounce2D via a
        // capability behavior instead of a barrier-template method.
        if (!this.TryGetBehavior<ISurfaceVelocity2D>(out _))
            this.GetOrAddBehavior<SurfaceVelocityProvider>();
    }

    public override void Update(in UpdateContext context)
    {
        // NaN doubles as a "first frame" flag: on initial Update we
        // seed CurrentAngleDeg and skip edge detection so an arm
        // constructed with Pressed=true doesn't immediately fire
        // OnPressed before the caller has finished wiring things up.
        bool firstFrame = float.IsNaN(CurrentAngleDeg);
        if (firstFrame)
        {
            CurrentAngleDeg = RestAngleDeg;
        }
        else if (Pressed != _wasPressed)
        {
            if (Pressed) 
                OnPressed(context);
            else 
                OnReleased(context);
        }
        _wasPressed = Pressed;

        var target = Pressed ? ActiveAngleDeg : RestAngleDeg;
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        var maxStep = SnapDegPerSec * dt;

        var delta = target - CurrentAngleDeg;
        float step;
        if (MathF.Abs(delta) <= maxStep)
            step = delta;
        else
            step = MathF.Sign(delta) * maxStep;

        CurrentAngleDeg += step;
        AngularVelRadPerSec = dt > 0f ? (step * MathF.PI / 180f) / dt : 0f;

        // Publish the swing to the collider: the capsule rides the Transform.
        Transform.Position = Pivot;
        Transform.Rotation = CurrentAngleDeg;
    }

    /// <summary>
    /// Called once on the frame <see cref="Pressed"/> transitions from
    /// <c>false</c> to <c>true</c>. Override to play a sound, emit
    /// particles, etc.
    /// </summary>
    protected virtual void OnPressed(in UpdateContext context) { }

    /// <summary>
    /// Called once on the frame <see cref="Pressed"/> transitions from
    /// <c>true</c> to <c>false</c>.
    /// </summary>
    protected virtual void OnReleased(in UpdateContext context) { }

    /// <summary>
    /// Provider behavior that exposes the swinging capsule's surface velocity
    /// (the 2D analog of ω × r about the pivot) to <see cref="SurfaceBounce2D"/>
    /// so a moving flipper transfers energy to the ball.
    /// </summary>
    private sealed class SurfaceVelocityProvider : Behavior, ISurfaceVelocity2D
    {
        private SwingArmBarrier2D _arm = null!;

        protected override void OnAttach(IEntity entity) => _arm = (SwingArmBarrier2D)entity;

        public Vector2 SurfaceVelocityAt(Vector2 point)
        {
            var offset = point - _arm.Pivot;
            // 2D analog of ω × r: rotate offset 90° CCW (in math frame)
            // and scale by signed angular velocity.
            return _arm.AngularVelRadPerSec * new Vector2(-offset.Y, offset.X);
        }
    }

    internal static (Vector2 point, float t) ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        var ab = b - a;
        var lenSq = Vector2.Dot(ab, ab);
        if (lenSq <= float.Epsilon)
            return (a, 0f);
        var t = Vector2.Dot(p - a, ab) / lenSq;
        if (t < 0f) t = 0f;
        else if (t > 1f) t = 1f;
        return (a + ab * t, t);
    }
}
